using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure.Crud;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

internal static class EquiposEndpoints
{
    public static void MapEquiposEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCrud("/api/v1/equipos", new CrudResource<Equipo, EquipoDto, CreateEquipoRequest, UpdateEquipoRequest>
        {
            Id = e => e.Id,
            ToDto = e => e.ToDto(),
            Search = (query, term) => query.Where(e => e.Nombre.ToLower().Contains(term.ToLower())),
            Create = request => new Equipo
            {
                Nombre = request.Nombre,
                Descripcion = request.Descripcion,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            ApplyUpdate = (equipo, request) =>
            {
                if (request.Nombre is not null) equipo.Nombre = request.Nombre;
                if (request.Descripcion is not null) equipo.Descripcion = request.Descripcion;
                if (request.Activo is not null) equipo.Activo = request.Activo.Value;
                equipo.UpdatedAt = DateTimeOffset.UtcNow;
            },
            Authorize = RpaFleetAuthorization.RequireManageAsync,
            BeforeCreate = async (ctx, ct) =>
            {
                var nombreTomado = await ctx.Db.Set<Equipo>().AnyAsync(e => e.Nombre == ctx.Entity.Nombre, ct);
                return nombreTomado ? ProblemResults.Conflict(ctx.Http, "Ya existe un equipo con ese nombre.") : null;
            },
            BeforeUpdate = async (ctx, request, ct) =>
            {
                if (request.Nombre is null || request.Nombre == ctx.Entity.Nombre) return null;
                var nombreTomado = await ctx.Db.Set<Equipo>().AnyAsync(e => e.Id != ctx.Entity.Id && e.Nombre == request.Nombre, ct);
                return nombreTomado ? ProblemResults.Conflict(ctx.Http, "Ya existe un equipo con ese nombre.") : null;
            },
            BeforeDelete = async (ctx, ct) =>
            {
                var enUso = await ctx.Db.Set<Despliegue>().AnyAsync(d => d.EquipoId == ctx.Entity.Id, ct);
                return enUso ? ProblemResults.Conflict(ctx.Http, "No se puede eliminar un equipo con despliegues.") : null;
            },
        });
    }
}
