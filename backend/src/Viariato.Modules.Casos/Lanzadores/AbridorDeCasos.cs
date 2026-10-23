using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Casos.Lanzadores;

/// <summary>What came of trying to open a Caso: its id, or the reason it could not be opened (written for a person to fix).</summary>
public sealed record AperturaDeCaso(Guid? CasoId, string? Error);

/// <summary>
/// Opens a Caso from a creator with nobody asking: no user, no HTTP. The data are the creator's defaults with the launcher's laid
/// over them, field by field. It makes the same checks a person's request goes through (the process is active and published, the
/// data fit the creator's form, the estado and the step exist) and the Caso is born the same way,
/// attributed to no user. Something that is wrong is answered, not thrown: a launcher that cannot open its Caso says why and tries
/// again at its next moment.
/// </summary>
public sealed class AbridorDeCasos(AppDbContext db, IEjecucionOrchestrator orquestador)
{
    public async Task<AperturaDeCaso> AbrirAsync(Guid creadorId, string? datosDelLanzador, Guid lanzadorId, DateTimeOffset cuando, CancellationToken ct)
    {
        var creador = await db.Set<CreadorDeCaso>().Include(c => c.Flujo).Include(c => c.TipoCaso).Include(c => c.Formulario)
            .FirstOrDefaultAsync(c => c.Id == creadorId, ct);
        if (creador is null) return Error("El creador ya no existe.");
        if (!creador.Activo) return Error($"El creador «{creador.Nombre}» está inactivo.");

        var flujo = creador.Flujo!;
        if (!flujo.Activo) return Error($"El proceso «{flujo.Nombre}» está desactivado.");
        if (flujo.VersionActivaId is not { } versionId) return Error($"El proceso «{flujo.Nombre}» no tiene una versión publicada.");

        var version = await db.Set<FlujoVersion>().AsNoTracking().FirstAsync(v => v.Id == versionId, ct);
        if (version.Estado != FlujoVersionEstado.Publicada) return Error("La versión activa del proceso no está publicada.");

        var tipo = creador.TipoCaso;
        if (creador.TipoCasoId is not null && (tipo is null || !tipo.Activo)) return Error("El tipo de caso del creador ya no está activo.");
        var datosJson = MezclaDeDatos.Mezclar(creador.DatosPorDefectoJson, datosDelLanzador);
        if (creador.Formulario is { } formulario)
        {
            var errores = EsquemaDatos.ValidarDatos(formulario.EsquemaJson, datosJson);
            if (errores.Count > 0) return Error($"Los datos no encajan con el formulario «{formulario.Nombre}»: {string.Join(" ", errores)}");
        }

        FlujoEstadoDef? estado = null;
        if (creador.EstadoNegocioInicialId is { } estadoId)
        {
            estado = await db.Set<FlujoEstadoDef>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == estadoId && e.FlujoId == flujo.Id && e.Activo, ct);
            if (estado is null) return Error("El estado de negocio inicial del creador ya no está activo.");
        }

        Guid? pasoInicialId = null;
        if (!string.IsNullOrWhiteSpace(creador.PasoInicialNombre))
        {
            var nombrePaso = creador.PasoInicialNombre;
            pasoInicialId = await db.Set<FlujoPasoDef>().AsNoTracking()
                .Where(p => p.FlujoVersionId == version.Id && p.Nombre == nombrePaso)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
            if (pasoInicialId is null) return Error($"La versión activa del proceso ya no tiene el paso «{nombrePaso}»: revisa el creador «{creador.Nombre}».");
        }

        var titulo = await TituloDeCreador.SiguienteAsync(db, creador, cuando, datosJson, ct);

        var ahora = DateTimeOffset.UtcNow;
        var caso = new Caso
        {
            FlujoId = flujo.Id,
            FlujoVersionId = version.Id,
            Titulo = titulo,
            DatosJson = string.IsNullOrWhiteSpace(datosJson) ? null : datosJson,
            TipoCasoId = tipo?.Id,
            FormularioId = creador.FormularioId,
            EstadoNegocioActualId = estado?.Id,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.Add(caso);
        db.Add(new CasoEvento
        {
            CasoId = caso.Id, Accion = CasoEventoAccion.Creado, OccurredAt = ahora,
            DetalleJson = JsonSerializer.Serialize(new { creadoPorLanzadorId = lanzadorId, creadorId = creador.Id }),
        });
        if (estado is not null)
        {
            db.Add(new CasoEvento
            {
                CasoId = caso.Id, Accion = CasoEventoAccion.EstadoNegocioActualizado, OccurredAt = ahora,
                DetalleJson = JsonSerializer.Serialize(new { estado.Codigo, estado.Display }),
            });
        }

        await db.SaveChangesAsync(ct);

        try
        {
            await orquestador.IniciarCasoAsync(caso.Id, pasoInicialId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new AperturaDeCaso(caso.Id, ex.Message);
        }

        return new AperturaDeCaso(caso.Id, null);
    }

    private static AperturaDeCaso Error(string mensaje) => new(null, mensaje);
}
