using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// A case type can describe its business data as a form (see <see cref="EsquemaDatos"/>); whoever writes the data —
/// a person, a launcher robot, an API client — gets the same answer the form would have given. A type without a
/// schema, and a Caso without a type, accept any JSON object as before.
/// </summary>
internal static class DatosPorTipo
{
    public static async Task<IResult?> RechazarSiNoCumpleAsync(AppDbContext db, Guid? tipoCasoId, string? datosJson, CancellationToken ct)
    {
        if (tipoCasoId is null) return null;

        var esquema = await db.Set<FlujoTipoCasoDef>().AsNoTracking()
            .Where(t => t.Id == tipoCasoId)
            .Select(t => t.EsquemaDatosJson)
            .FirstOrDefaultAsync(ct);

        return Rechazar(esquema, datosJson);
    }

    public static IResult? Rechazar(string? esquemaJson, string? datosJson)
    {
        if (string.IsNullOrWhiteSpace(esquemaJson)) return null;

        var errores = EsquemaDatos.ValidarDatos(esquemaJson, datosJson);
        return errores.Count == 0
            ? null
            : ProblemResults.ValidationProblem(new ValidationResult(errores.Select(e => new ValidationFailure("DatosJson", e))));
    }
}
