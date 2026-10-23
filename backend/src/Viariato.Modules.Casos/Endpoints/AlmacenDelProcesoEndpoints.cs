using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <param name="Bytes">Size of the stored JSON, against the 64 KB a key can hold.</param>
public sealed record DatoDeProcesoResumenDto(string Clave, int Bytes, DateTimeOffset ActualizadoEn);

public sealed record DatoDeProcesoDto(string Clave, string ValorJson, DateTimeOffset ActualizadoEn);

/// <summary>
/// The people's side of a process's store (see <see cref="DatoDeProceso"/>): whoever can read the process sees what its robots
/// remember between cases. Read-only: the robots own it, and a value changed by hand would only confuse the next case.
/// </summary>
internal static class AlmacenDelProcesoEndpoints
{
    public static void MapAlmacenDelProcesoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/almacen").RequireAuthorization(Permissions.FlujosRead);
        group.MapGet("/", ListAsync);
        // The key goes in the query string: it may hold ':' and other signs a route segment handles badly.
        group.MapGet("/dato", LeerAsync);
    }

    private static async Task<IResult> ListAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        // The value is jsonb, which has no length of its own: its size is measured as text, without bringing the values over.
        var datos = await db.Database
            .SqlQuery<DatoDeProcesoResumenDto>($"""
                select clave, octet_length(valor_json::text) as bytes, updated_at as actualizado_en
                from casos.datos_de_proceso where flujo_id = {flujoId} order by clave
                """)
            .ToListAsync(ct);

        return Results.Ok(datos);
    }

    private static async Task<IResult> LeerAsync(Guid flujoId, string clave, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var dato = await db.Set<DatoDeProceso>().AsNoTracking().FirstOrDefaultAsync(d => d.FlujoId == flujoId && d.Clave == clave, ct);
        return dato is null
            ? ProblemResults.NotFound(http, "No hay nada guardado con esa clave.")
            : Results.Ok(new DatoDeProcesoDto(dato.Clave, dato.ValorJson, dato.UpdatedAt));
    }
}
