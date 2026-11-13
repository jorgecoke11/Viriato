using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.RpaFleet.Lanzador;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

/// <summary>
/// The robot's program, uploaded to its Despliegue: the launcher next to Viriato runs the current version. Uploading one is the
/// whole deployment of a robot — how to run it is worked out from the zip (<see cref="PaqueteDeRobot"/>).
/// </summary>
internal static class VersionesDeRobotEndpoints
{
    public static void MapVersionesDeRobotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/despliegues/{id:guid}").RequireAuthorization(Permissions.RpaManage);

        group.MapGet("/versiones", ListAsync);
        group.MapPost("/versiones", SubirAsync)
            .DisableAntiforgery()
            // Kestrel's 30 MB default is less than a robot with its browser libraries; the ceiling is checked below, with a message.
            .WithMetadata(new RequestSizeLimitAttribute(1024L * 1024 * 1024));
        group.MapPut("/version-actual", UsarAsync);
        group.MapDelete("/versiones/{versionId:guid}", BorrarAsync);
    }

    private static async Task<IResult> ListAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        var versiones = await db.Set<VersionDeRobot>().AsNoTracking()
            .Where(v => v.DespliegueId == id).OrderByDescending(v => v.Numero).ToListAsync(ct);
        return Results.Ok(versiones.Select(v => v.ToDto(despliegue.VersionDeRobotId)).ToList());
    }

    /// <summary>Uploads a version and makes it the one that runs: the launcher picks it up on its next round.</summary>
    private static async Task<IResult> SubirAsync(
        Guid id,
        IFormFile? file,
        ClaimsPrincipal principal,
        AppDbContext db,
        AlmacenDeRobots almacen,
        IOptions<LanzadorOptions> opciones,
        HttpContext http,
        CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        if (file is null || file.Length == 0) return Invalido("Elige el .zip del robot.");
        var maximo = Math.Max(1, opciones.Value.TamanoMaximoMb);
        if (file.Length > maximo * 1024L * 1024L) return Invalido($"El .zip pesa más de {maximo} MB.");

        // Worked on from a temporary copy: the analysis and the hash read it, then it is stored.
        var temporal = Path.GetTempFileName();
        try
        {
            await using (var destino = File.Create(temporal))
            {
                await file.CopyToAsync(destino, ct);
            }

            ComoSeEjecuta como;
            string sha;
            await using (var zip = File.OpenRead(temporal))
            {
                var (analizado, problema) = PaqueteDeRobot.Analizar(zip);
                if (analizado is null) return Invalido(problema!);
                como = analizado;
                zip.Position = 0;
                sha = Convert.ToHexString(await SHA256.HashDataAsync(zip, ct)).ToLowerInvariant();
            }

            var numero = (await db.Set<VersionDeRobot>().Where(v => v.DespliegueId == id).MaxAsync(v => (int?)v.Numero, ct) ?? 0) + 1;
            var version = new VersionDeRobot
            {
                DespliegueId = id,
                Numero = numero,
                NombreArchivo = Path.GetFileName(file.FileName) is { Length: > 0 } nombre ? nombre[..Math.Min(nombre.Length, 260)] : "robot.zip",
                TamanoBytes = file.Length,
                Sha256 = sha,
                Ruta = string.Empty,
                Raiz = como.Raiz,
                Comando = como.Comando,
                Argumentos = [.. como.Argumentos],
                Pantalla = como.Pantalla,
                MemoriaMaximaMb = como.MemoriaMaximaMb,
                MaxCopias = como.MaxCopias,
                EntornoJson = como.Entorno.Count > 0 ? JsonSerializer.Serialize(como.Entorno) : null,
                SubidaPor = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity?.Name,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await using (var zip = File.OpenRead(temporal))
            {
                version.Ruta = await almacen.GuardarAsync(id, version.Id, zip, ct);
            }

            db.Add(version);
            despliegue.VersionDeRobotId = version.Id;
            despliegue.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(version.ToDto(version.Id));
        }
        finally
        {
            File.Delete(temporal);
        }
    }

    /// <summary>Goes back (or forward) to a version, or — with none — stops the launcher running this Despliegue.</summary>
    private static async Task<IResult> UsarAsync(Guid id, UsarVersionDeRobotRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        if (request.VersionId is { } versionId
            && !await db.Set<VersionDeRobot>().AnyAsync(v => v.Id == versionId && v.DespliegueId == id, ct))
        {
            return ProblemResults.NotFound(http, "Esa versión no es de este despliegue.");
        }

        despliegue.VersionDeRobotId = request.VersionId;
        despliegue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> BorrarAsync(
        Guid id, Guid versionId, AppDbContext db, AlmacenDeRobots almacen, HttpContext http, CancellationToken ct)
    {
        var version = await db.Set<VersionDeRobot>().FirstOrDefaultAsync(v => v.Id == versionId && v.DespliegueId == id, ct);
        if (version is null) return ProblemResults.NotFound(http, "Versión no encontrada.");

        if (await db.Set<Despliegue>().AnyAsync(d => d.Id == id && d.VersionDeRobotId == versionId, ct))
        {
            return ProblemResults.Conflict(http, "Es la versión que se está usando: elige otra antes de borrarla.");
        }

        db.Remove(version);
        await db.SaveChangesAsync(ct);
        almacen.Borrar(version.Ruta);
        return Results.NoContent();
    }

    private static IResult Invalido(string motivo) => Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [motivo] });
}
