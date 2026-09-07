using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure.Crud;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// Global catalog of document classification types (e.g. "DNI", "Nómina") — not scoped to a Flujo,
/// referenced by DocumentoClasificacion. Gated entirely behind CasosManage: reviewers with only
/// CasosRead see classification names denormalized on DocumentoDto and never need this list directly.
/// </summary>
internal static class TiposDocumentoEndpoints
{
    public static void MapTiposDocumentoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCrud("/api/v1/tipos-documento", new CrudResource<TipoDocumento, TipoDocumentoDto, CreateTipoDocumentoRequest, UpdateTipoDocumentoRequest>
        {
            Id = t => t.Id,
            ToDto = t => t.ToDto(),
            Search = (query, term) => query.Where(t => t.Nombre.ToLower().Contains(term.ToLower())),
            Create = request => new TipoDocumento
            {
                Nombre = request.Nombre,
                Descripcion = request.Descripcion,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            ApplyUpdate = (tipo, request) =>
            {
                if (request.Nombre is not null) tipo.Nombre = request.Nombre;
                if (request.Descripcion is not null) tipo.Descripcion = request.Descripcion;
                if (request.Activo is not null) tipo.Activo = request.Activo.Value;
                tipo.UpdatedAt = DateTimeOffset.UtcNow;
            },
            Authorize = RequireCasosManageAsync,
            BeforeCreate = async (ctx, ct) =>
            {
                var nombreTomado = await ctx.Db.Set<TipoDocumento>().AnyAsync(t => t.Nombre == ctx.Entity.Nombre, ct);
                return nombreTomado ? ProblemResults.Conflict(ctx.Http, "Ya existe un tipo de documento con ese nombre.") : null;
            },
            BeforeUpdate = async (ctx, request, ct) =>
            {
                if (request.Nombre is null || request.Nombre == ctx.Entity.Nombre) return null;
                var nombreTomado = await ctx.Db.Set<TipoDocumento>().AnyAsync(t => t.Id != ctx.Entity.Id && t.Nombre == request.Nombre, ct);
                return nombreTomado ? ProblemResults.Conflict(ctx.Http, "Ya existe un tipo de documento con ese nombre.") : null;
            },
            BeforeDelete = async (ctx, ct) =>
            {
                var enUso = await ctx.Db.Set<DocumentoClasificacion>().AnyAsync(c => c.TipoDocumentoId == ctx.Entity.Id, ct);
                return enUso ? ProblemResults.Conflict(ctx.Http, "No se puede eliminar un tipo de documento en uso.") : null;
            },
        });
    }

    private static Task<IResult?> RequireCasosManageAsync(HttpContext http, CancellationToken ct)
    {
        var allowed = http.User.HasClaim("perm", Permissions.CasosManage);
        return Task.FromResult<IResult?>(allowed ? null : ProblemResults.Forbidden(http, "No tienes permiso para gestionar tipos de documento."));
    }
}
