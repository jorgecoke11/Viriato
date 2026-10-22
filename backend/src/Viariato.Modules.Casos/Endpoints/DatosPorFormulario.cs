using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// A Caso's business data can be described by a form (see <see cref="Formulario"/>); whoever writes the data — a person, a launcher,
/// a robot, an API client — gets the same answer the form would have given. A Caso without a form accepts any JSON object.
/// </summary>
internal static class DatosPorFormulario
{
    public static async Task<IResult?> RechazarSiNoCumpleAsync(AppDbContext db, Guid? formularioId, string? datosJson, CancellationToken ct)
    {
        if (formularioId is null) return null;

        var esquema = await db.Set<Formulario>().AsNoTracking()
            .Where(f => f.Id == formularioId)
            .Select(f => f.EsquemaJson)
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
