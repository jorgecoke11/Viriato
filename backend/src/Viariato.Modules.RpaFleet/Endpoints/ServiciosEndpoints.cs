using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure.Crud;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

internal static class ServiciosEndpoints
{
    public static void MapServiciosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCrud("/api/v1/servicios", new CrudResource<Servicio, ServicioDto, CreateServicioRequest, UpdateServicioRequest>
        {
            Id = s => s.Id,
            ToDto = s => s.ToDto(),
            Search = (query, term) => query.Where(s => s.Nombre.ToLower().Contains(term.ToLower())),
            Create = request => new Servicio
            {
                Nombre = request.Nombre,
                Descripcion = request.Descripcion,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            ApplyUpdate = (servicio, request) =>
            {
                if (request.Nombre is not null) servicio.Nombre = request.Nombre;
                if (request.Descripcion is not null) servicio.Descripcion = request.Descripcion;
                if (request.Activo is not null) servicio.Activo = request.Activo.Value;
                servicio.UpdatedAt = DateTimeOffset.UtcNow;
            },
            Authorize = RpaFleetAuthorization.RequireManageAsync,
            BeforeCreate = async (ctx, ct) =>
            {
                var nombreTomado = await ctx.Db.Set<Servicio>().AnyAsync(s => s.Nombre == ctx.Entity.Nombre, ct);
                return nombreTomado ? ProblemResults.Conflict(ctx.Http, "Ya existe un servicio con ese nombre.") : null;
            },
            BeforeUpdate = async (ctx, request, ct) =>
            {
                if (request.Nombre is null || request.Nombre == ctx.Entity.Nombre) return null;
                var nombreTomado = await ctx.Db.Set<Servicio>().AnyAsync(s => s.Id != ctx.Entity.Id && s.Nombre == request.Nombre, ct);
                return nombreTomado ? ProblemResults.Conflict(ctx.Http, "Ya existe un servicio con ese nombre.") : null;
            },
            BeforeDelete = async (ctx, ct) =>
            {
                var enUso = await ctx.Db.Set<Despliegue>().AnyAsync(d => d.ServicioId == ctx.Entity.Id, ct);
                return enUso ? ProblemResults.Conflict(ctx.Http, "No se puede eliminar un servicio con despliegues.") : null;
            },
        });
    }
}
