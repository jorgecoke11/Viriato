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
using Viariato.Modules.RpaFleet.Persistence;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

/// <summary>
/// The robot's program, uploaded to its Servicio: every Despliegue of it that the launcher next to Viriato runs gets the latest
/// version (or the one it is held at). How to run it is worked out from the zip (<see cref="PaqueteDeRobot"/>).
/// </summary>
internal static class VersionesDeRobotEndpoints
{
    public static void MapVersionesDeRobotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var servicio = endpoints.MapGroup("/api/v1/servicios/{id:guid}/versiones").RequireAuthorization(Permissions.RpaManage);
        servicio.MapGet("/", ListAsync);
        servicio.MapPost("/", SubirAsync)
            .DisableAntiforgery()
            // Kestrel's 30 MB default is less than a robot with its browser libraries; the ceiling is checked below, with a message.
            .WithMetadata(new RequestSizeLimitAttribute(1024L * 1024 * 1024));
        servicio.MapDelete("/{versionId:guid}", BorrarAsync);

        var despliegue = endpoints.MapGroup("/api/v1/despliegues/{id:guid}").RequireAuthorization(Permissions.RpaManage);
        despliegue.MapPut("/robot", RobotDelDespliegueAsync);
        despliegue.MapPut("/lanzador", RobotEnElLanzadorAsync);

        var ajustes = endpoints.MapGroup("/api/v1/lanzador/ajustes").RequireAuthorization(Permissions.RpaManage);
        ajustes.MapGet("/", GetAjustesAsync);
        ajustes.MapPut("/", PutAjustesAsync);
    }

    public const int MaxReplicas = 10;
    public const int MemoriaMinimaMb = 128;

    /// <summary>Replicas and memory ceiling of this robot. The launcher applies them next round.</summary>
    private static async Task<IResult> RobotEnElLanzadorAsync(
        Guid id, RobotEnElLanzadorRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        if (request.Replicas is < 0 or > MaxReplicas) errores["replicas"] = [$"Entre 0 (aparcado) y {MaxReplicas}."];
        if (request.MemoriaMaximaMb is { } mb && mb < MemoriaMinimaMb) errores["memoriaMaximaMb"] = [$"Al menos {MemoriaMinimaMb} MB."];
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        var despliegue = await db.Set<Despliegue>().ConSuRobot().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        despliegue.Replicas = request.Replicas;
        despliegue.MemoriaMaximaMb = request.MemoriaMaximaMb;
        despliegue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(despliegue.ToDto());
    }

    /// <summary>Whether the launcher runs this Despliegue's robot, and with the latest version of its service or a fixed one.</summary>
    private static async Task<IResult> RobotDelDespliegueAsync(
        Guid id, RobotDelDespliegueRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().ConSuRobot().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        if (request.VersionId is { } versionId && despliegue.Servicio!.Versiones.All(v => v.Id != versionId))
        {
            return ProblemResults.NotFound(http, "Esa versión no es de su servicio.");
        }

        despliegue.EnElLanzador = request.EnElLanzador;
        despliegue.VersionDeRobotId = request.VersionId;
        despliegue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(despliegue.ToDto());
    }

    private static async Task<IResult> GetAjustesAsync(AppDbContext db, CancellationToken ct) =>
        Results.Ok(ToDto(await db.Set<AjustesDelLanzador>().AsNoTracking().FirstOrDefaultAsync(ct) ?? new AjustesDelLanzador()));

    private static async Task<IResult> PutAjustesAsync(AjustesDelLanzadorDto request, AppDbContext db, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        if (request.MaxRobotsALaVez is < 1 or > 20) errores["maxRobotsALaVez"] = ["Entre 1 y 20."];
        if (request.MaxConNavegador < 0) errores["maxConNavegador"] = ["No puede ser negativo."];
        else if (request.MaxConNavegador > request.MaxRobotsALaVez) errores["maxConNavegador"] = ["No puede ser más que los robots a la vez."];
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        var ajustes = await db.Set<AjustesDelLanzador>().FirstOrDefaultAsync(ct);
        if (ajustes is null) db.Add(ajustes = new AjustesDelLanzador());
        ajustes.MaxRobotsALaVez = request.MaxRobotsALaVez;
        ajustes.MaxConNavegador = request.MaxConNavegador;
        ajustes.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(ajustes));
    }

    private static AjustesDelLanzadorDto ToDto(AjustesDelLanzador a) => new(a.MaxRobotsALaVez, a.MaxConNavegador);

    private static async Task<IResult> ListAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Servicio>().AnyAsync(s => s.Id == id, ct)) return ProblemResults.NotFound(http, "Servicio no encontrado.");

        var versiones = await db.Set<VersionDeRobot>().AsNoTracking()
            .Where(v => v.ServicioId == id).OrderByDescending(v => v.Numero).ToListAsync(ct);
        var despliegues = await db.Set<Despliegue>().AsNoTracking().ConSuRobot()
            .Where(d => d.ServicioId == id && d.EnElLanzador).ToListAsync(ct);
        var ultima = versiones.FirstOrDefault()?.Id;

        return Results.Ok(versiones.Select(v => v.ToDto(
            v.Id == ultima,
            despliegues.Where(d => d.VersionQueCorre?.Id == v.Id).Select(d => d.Equipo!.Nombre).Order().ToList())).ToList());
    }

    /// <summary>Uploads a version: every Despliegue of the service that follows the latest runs it from the launcher's next round.</summary>
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
        if (!await db.Set<Servicio>().AnyAsync(s => s.Id == id, ct)) return ProblemResults.NotFound(http, "Servicio no encontrado.");

        if (file is null || file.Length == 0) return ProblemResults.ValidationProblem("file", "Elige el .zip del robot.");
        var maximo = Math.Max(1, opciones.Value.TamanoMaximoMb);
        if (file.Length > maximo * 1024L * 1024L) return ProblemResults.ValidationProblem("file", $"El .zip pesa más de {maximo} MB.");

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
                if (analizado is null) return ProblemResults.ValidationProblem("file", problema!);
                como = analizado;
                zip.Position = 0;
                sha = Convert.ToHexString(await SHA256.HashDataAsync(zip, ct)).ToLowerInvariant();
            }

            var numero = (await db.Set<VersionDeRobot>().Where(v => v.ServicioId == id).MaxAsync(v => (int?)v.Numero, ct) ?? 0) + 1;
            var version = new VersionDeRobot
            {
                ServicioId = id,
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
                EntornoJson = como.Entorno.Count > 0 ? JsonSerializer.Serialize(como.Entorno) : null,
                SubidaPor = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity?.Name,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await using (var zip = File.OpenRead(temporal))
            {
                version.Ruta = await almacen.GuardarAsync(id, version.Id, zip, ct);
            }

            db.Add(version);
            await db.SaveChangesAsync(ct);

            var usadaPor = await db.Set<Despliegue>().AsNoTracking()
                .Where(d => d.ServicioId == id && d.EnElLanzador && d.VersionDeRobotId == null)
                .Select(d => d.Equipo!.Nombre).OrderBy(n => n).ToListAsync(ct);
            return Results.Ok(version.ToDto(ultima: true, usadaPor));
        }
        finally
        {
            File.Delete(temporal);
        }
    }

    private static async Task<IResult> BorrarAsync(
        Guid id, Guid versionId, AppDbContext db, AlmacenDeRobots almacen, HttpContext http, CancellationToken ct)
    {
        var version = await db.Set<VersionDeRobot>().FirstOrDefaultAsync(v => v.Id == versionId && v.ServicioId == id, ct);
        if (version is null) return ProblemResults.NotFound(http, "Versión no encontrada.");

        // In use: held at it, or it is the latest and a Despliegue in the launcher follows it. Going back is holding them at another.
        var enUso = await db.Set<Despliegue>().AsNoTracking().ConSuRobot().Where(d => d.ServicioId == id).ToListAsync(ct);
        if (enUso.Any(d => d.VersionDeRobotId == versionId || d.VersionQueCorre?.Id == versionId))
        {
            return ProblemResults.Conflict(http, "Hay despliegues usando esta versión: fíjalos en otra antes de borrarla.");
        }

        db.Remove(version);
        await db.SaveChangesAsync(ct);
        almacen.Borrar(version.Ruta);
        return Results.NoContent();
    }

}
