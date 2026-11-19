using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Familias;

/// <summary>Something asked of a family that cannot be done, said for a person (or a robot's log) to fix.</summary>
public sealed class FamiliaNoValidaException(string mensaje, bool noEncontrada = false) : Exception(mensaje)
{
    public bool NoEncontrada { get; } = noEncontrada;
}

/// <summary>
/// What people and robots do with families — create one, put cases in it, close it — with the same rules whoever asks. Ending a family
/// is not here: that is the finisher's (<see cref="TerminadorDeFamilias"/>), apart, so it happens once whoever closed it.
/// Changes are added to the context; the caller saves (a robot's new case and its membership go in one save).
/// </summary>
public sealed class GestorDeFamilias(AppDbContext db)
{
    public const int MaximoDeParticipantes = 100_000;

    /// <param name="tipo">The family type, by id or by name (robots name it).</param>
    public async Task<Familia> CrearAsync(
        Guid flujoId, string tipo, string nombre, int? participantesEsperados, Guid? userId, Guid? despliegueId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > 200)
        {
            throw new FamiliaNoValidaException("El nombre de la familia es obligatorio, de 200 letras como mucho.");
        }

        if (participantesEsperados is < 1 or > MaximoDeParticipantes)
        {
            throw new FamiliaNoValidaException($"Los participantes esperados van de 1 a {MaximoDeParticipantes}, o ninguno.");
        }

        var tipoDeFamilia = await BuscarTipoAsync(flujoId, tipo, ct)
            ?? throw new FamiliaNoValidaException($"El proceso no tiene el tipo de familia «{tipo}».");
        if (!tipoDeFamilia.Activo) throw new FamiliaNoValidaException($"El tipo de familia «{tipoDeFamilia.Nombre}» está desactivado.");

        var ahora = DateTimeOffset.UtcNow;
        var familia = new Familia
        {
            FlujoId = flujoId,
            TipoDeFamiliaId = tipoDeFamilia.Id,
            Tipo = tipoDeFamilia,
            Nombre = nombre.Trim(),
            ParticipantesEsperados = participantesEsperados,
            CreadaPorUserId = userId,
            CreadaPorDespliegueId = despliegueId,
            CreatedAt = ahora,
        };
        db.Add(familia);
        db.Add(new FamiliaEvento
        {
            FamiliaId = familia.Id,
            Accion = FamiliaEventoAccion.Creada,
            DetalleJson = JsonSerializer.Serialize(new { tipo = tipoDeFamilia.Nombre, participantesEsperados, userId, despliegueId }),
            OccurredAt = ahora,
        });
        return familia;
    }

    /// <summary>Puts cases in an open family of their process. Cases already in it are left as they are.</summary>
    public async Task AnadirAsync(Guid familiaId, IReadOnlyCollection<Guid> casoIds, Guid flujoId, CancellationToken ct)
    {
        var familia = await db.Set<Familia>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == familiaId, ct)
            ?? throw new FamiliaNoValidaException("Familia no encontrada.", noEncontrada: true);
        if (familia.FlujoId != flujoId) throw new FamiliaNoValidaException($"La familia «{familia.Nombre}» es de otro proceso.");
        if (familia.Estado != EstadoDeFamilia.Abierta)
        {
            throw new FamiliaNoValidaException($"La familia «{familia.Nombre}» ya está {(familia.Estado == EstadoDeFamilia.Cerrada ? "cerrada" : "terminada")}: no entran más casos.");
        }

        var yaEstan = await db.Set<MiembroDeFamilia>().AsNoTracking()
            .Where(m => m.FamiliaId == familiaId && casoIds.Contains(m.CasoId)).Select(m => m.CasoId).ToListAsync(ct);
        var nuevos = casoIds.Except(yaEstan).Except(EnEsteGuardado(familiaId)).Distinct().ToList();
        if (nuevos.Count == 0) return;

        if (familia.ParticipantesEsperados is { } esperados)
        {
            var actuales = await db.Set<MiembroDeFamilia>().CountAsync(m => m.FamiliaId == familiaId, ct) + EnEsteGuardado(familiaId).Count();
            if (actuales + nuevos.Count > esperados)
            {
                throw new FamiliaNoValidaException(
                    $"La familia «{familia.Nombre}» espera {esperados} casos y ya tiene {actuales}: no caben {nuevos.Count} más.");
            }
        }

        var ahora = DateTimeOffset.UtcNow;
        foreach (var casoId in nuevos)
        {
            db.Add(new MiembroDeFamilia { FamiliaId = familiaId, CasoId = casoId, AnadidoAt = ahora });
        }
    }

    /// <summary>No more cases join; the finisher ends it once its members have ended. Closing a closed family does nothing.</summary>
    public async Task CerrarAsync(Guid familiaId, Guid? flujoId, CancellationToken ct)
    {
        var familia = await db.Set<Familia>().FirstOrDefaultAsync(f => f.Id == familiaId, ct);
        if (familia is null || (flujoId is { } id && familia.FlujoId != id)) throw new FamiliaNoValidaException("Familia no encontrada.", noEncontrada: true);
        if (familia.Estado != EstadoDeFamilia.Abierta) return;

        var ahora = DateTimeOffset.UtcNow;
        familia.Estado = EstadoDeFamilia.Cerrada;
        familia.CerradaAt = ahora;
        var miembros = await db.Set<MiembroDeFamilia>().CountAsync(m => m.FamiliaId == familiaId, ct);
        db.Add(new FamiliaEvento
        {
            FamiliaId = familiaId,
            Accion = FamiliaEventoAccion.Cerrada,
            DetalleJson = JsonSerializer.Serialize(new { miembros, automatica = false }),
            OccurredAt = ahora,
        });
    }

    /// <summary>
    /// A robot working <paramref name="pasoOrigen"/> of a case has just created <paramref name="caso"/> (not saved yet): it goes into the
    /// families that form by themselves from that step — its parent case's open family of each such type of the new case's process (one
    /// per case type if the type says so), created with the first case. Each family follows the step's latest attempt, which is what
    /// closes it when it ends well (<see cref="TerminadorDeFamilias"/>).
    /// </summary>
    public async Task AnadirALasQueSeFormanSolasAsync(Caso caso, EjecucionPaso pasoOrigen, string nombrePaso, CancellationToken ct)
    {
        var tipos = await db.Set<TipoDeFamilia>()
            .Where(t => t.FlujoId == caso.FlujoId && t.Activo && t.PasoQueLaForma != null && t.PasoQueLaForma.ToLower() == nombrePaso.ToLower())
            .ToListAsync(ct);
        if (tipos.Count == 0) return;

        var padre = await db.Set<Caso>().AsNoTracking().Where(c => c.Id == pasoOrigen.CasoId).Select(c => c.Titulo).FirstAsync(ct);
        var ahora = DateTimeOffset.UtcNow;
        foreach (var tipo in tipos)
        {
            var tipoCasoId = tipo.UnaPorTipoDeCaso ? caso.TipoCasoId : null;
            var familia = db.ChangeTracker.Entries<Familia>().Select(e => e.Entity)
                    .FirstOrDefault(f => f.Estado == EstadoDeFamilia.Abierta && f.CasoOrigenId == pasoOrigen.CasoId && f.TipoDeFamiliaId == tipo.Id && f.TipoCasoId == tipoCasoId)
                ?? await db.Set<Familia>()
                    .Where(f => f.Estado == EstadoDeFamilia.Abierta && f.CasoOrigenId == pasoOrigen.CasoId && f.TipoDeFamiliaId == tipo.Id && f.TipoCasoId == tipoCasoId)
                    .OrderByDescending(f => f.CreatedAt)
                    .FirstOrDefaultAsync(ct);

            if (familia is null)
            {
                var nombre = padre;
                if (tipo.UnaPorTipoDeCaso)
                {
                    var tipoCaso = tipoCasoId is { } id
                        ? await db.Set<FlujoTipoCasoDef>().AsNoTracking().Where(t => t.Id == id).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
                        : null;
                    nombre = $"{padre} · {tipoCaso ?? "sin tipo"}";
                }

                familia = new Familia
                {
                    FlujoId = caso.FlujoId,
                    TipoDeFamiliaId = tipo.Id,
                    Tipo = tipo,
                    Nombre = nombre.Length > 200 ? nombre[..200] : nombre,
                    CasoOrigenId = pasoOrigen.CasoId,
                    TipoCasoId = tipoCasoId,
                    CreatedAt = ahora,
                };
                db.Add(familia);
                db.Add(new FamiliaEvento
                {
                    FamiliaId = familia.Id,
                    Accion = FamiliaEventoAccion.Creada,
                    DetalleJson = JsonSerializer.Serialize(new { tipo = tipo.Nombre, casoOrigenId = pasoOrigen.CasoId, paso = nombrePaso }),
                    OccurredAt = ahora,
                });
            }

            familia.PasoOrigenId = pasoOrigen.Id;
            db.Add(new MiembroDeFamilia { FamiliaId = familia.Id, CasoId = caso.Id, AnadidoAt = ahora });
        }
    }

    private IEnumerable<Guid> EnEsteGuardado(Guid familiaId) =>
        db.ChangeTracker.Entries<MiembroDeFamilia>().Where(e => e.State == EntityState.Added && e.Entity.FamiliaId == familiaId).Select(e => e.Entity.CasoId);

    private async Task<TipoDeFamilia?> BuscarTipoAsync(Guid flujoId, string tipo, CancellationToken ct)
    {
        var tipos = db.Set<TipoDeFamilia>().Where(t => t.FlujoId == flujoId);
        if (Guid.TryParse(tipo, out var id)) return await tipos.FirstOrDefaultAsync(t => t.Id == id, ct);

        var nombre = tipo.Trim();
        return await tipos.FirstOrDefaultAsync(t => t.Nombre == nombre, ct)
            ?? (await tipos.ToListAsync(ct)).FirstOrDefault(t => t.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
    }
}
