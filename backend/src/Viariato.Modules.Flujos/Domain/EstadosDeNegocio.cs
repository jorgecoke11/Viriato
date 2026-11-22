using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;

namespace Viariato.Modules.Flujos.Domain;

/// <summary>
/// The business states a robot names by code. A robot may use a state its process does not define yet: it is created on the spot,
/// so nobody has to add every state by hand before a robot can report it. One an admin switched off is the exception: it stays off
/// (switching a state off is how an admin stops a robot from using it), and the caller treats it as not found. The same goes for the
/// flow editor: an arrow may name a state no robot has used yet, and saving the version creates it.
/// </summary>
public static partial class EstadosDeNegocio
{
    /// <summary>The same rule the admin screen applies when a state is created there.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{1,50}$")]
    private static partial Regex CodigoValido();

    public static bool EsCodigoValido(string codigo) => CodigoValido().IsMatch(codigo);

    /// <summary>
    /// The active state <paramref name="codigo"/> of the process, created if the process has none with that code. Null when the code
    /// is not a valid one or the state exists but is switched off. <c>Creado</c> says whether this call created it. Saves on its own
    /// only when it creates; a robot racing another to create the same state ends up with the one that won.
    /// </summary>
    public static async Task<(FlujoEstadoDef? Estado, bool Creado)> ObtenerOCrearAsync(
        AppDbContext db, Guid flujoId, string codigo, CancellationToken ct)
    {
        codigo = codigo.Trim();
        if (!EsCodigoValido(codigo)) return (null, false);

        var existente = await db.Set<FlujoEstadoDef>().FirstOrDefaultAsync(e => e.FlujoId == flujoId && e.Codigo == codigo, ct);
        if (existente is not null) return (existente.Activo ? existente : null, false);

        var maxOrden = await db.Set<FlujoEstadoDef>().Where(e => e.FlujoId == flujoId).MaxAsync(e => (int?)e.Orden, ct) ?? 0;
        var ahora = DateTimeOffset.UtcNow;
        var nuevo = new FlujoEstadoDef
        {
            FlujoId = flujoId,
            Codigo = codigo,
            Display = DisplayDesdeCodigo(codigo),
            Orden = maxOrden + 1,
            EsFinal = false,
            Activo = true,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.Add(nuevo);

        try
        {
            await db.SaveChangesAsync(ct);
            return (nuevo, true);
        }
        catch (DbUpdateException)
        {
            // Another robot created it between the read and the save (unique FlujoId + Codigo): use theirs.
            db.Entry(nuevo).State = EntityState.Detached;
            var ganador = await db.Set<FlujoEstadoDef>().FirstOrDefaultAsync(e => e.FlujoId == flujoId && e.Codigo == codigo, ct);
            if (ganador is null) throw;
            return (ganador.Activo ? ganador : null, false);
        }
    }

    /// <summary><c>PRECIOS_EXTRAIDOS_SIN_ACTUALIZAR</c> → "Precios extraidos sin actualizar". An admin can rename it afterwards.</summary>
    public static string DisplayDesdeCodigo(string codigo)
    {
        var texto = string.Join(' ', codigo.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        return texto.Length == 0 ? codigo : char.ToUpperInvariant(texto[0]) + texto[1..];
    }
}
