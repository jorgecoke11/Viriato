using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Conexiones;
using Viariato.Modules.Conexiones.Domain;
using Viariato.Modules.Conexiones.Tipos;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Notificaciones.Domain;
using Viariato.Modules.Notificaciones.Envio;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Notificaciones.Endpoints;

public sealed record ReglaDeNotificacionDto(
    Guid Id, Guid FlujoId, Guid? TipoCasoId, string? TipoCasoNombre, string Nombre, bool Activa, string Disparador, string? Codigo,
    Guid ConexionId, string ConexionNombre, IReadOnlyList<string> Destinatarios, string Asunto, string Cuerpo,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record GuardarReglaRequest(
    string Nombre, bool Activa, string Disparador, string? Codigo, Guid? TipoCasoId, Guid ConexionId, IReadOnlyList<string>? Destinatarios,
    string Asunto, string Cuerpo);

public sealed record EnvioDeNotificacionDto(
    Guid Id, Guid? ReglaId, string ReglaNombre, Guid CasoId, string CasoTitulo, IReadOnlyList<string> Destinatarios, string Asunto, string Estado,
    int Intentos, string? UltimoError, DateTimeOffset CreatedAt, DateTimeOffset? EnviadoAt);

public sealed record ProbarReglaRequest(string? Destinatario);

/// <summary>The notification rules of a process (who configures the process configures its notices), and what they sent.</summary>
internal static partial class NotificacionesEndpoints
{
    [GeneratedRegex("^[a-z0-9_]{1,60}$")]
    private static partial Regex CodigoDeEvento();

    public static IEndpointRouteBuilder MapNotificacionesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/notificaciones").RequireAuthorization(Permissions.FlujosManage);
        group.MapGet("/", ListarAsync);
        group.MapPost("/", CrearAsync);
        group.MapPut("/{id:guid}", ActualizarAsync);
        group.MapDelete("/{id:guid}", BorrarAsync);
        group.MapPost("/{id:guid}/probar", ProbarAsync);
        group.MapGet("/envios", EnviosAsync);
        group.MapPost("/envios/{id:guid}/reintentar", ReintentarAsync);
        return endpoints;
    }

    private static async Task<IResult> ListarAsync(Guid flujoId, AppDbContext db, CancellationToken ct)
    {
        var reglas = await db.Set<ReglaDeNotificacion>().AsNoTracking().Where(r => r.FlujoId == flujoId).OrderBy(r => r.Nombre).ToListAsync(ct);
        var nombres = await NombresAsync(db, flujoId, ct);
        return Results.Ok(reglas.Select(r => ADto(r, nombres)));
    }

    private static async Task<IResult> CrearAsync(Guid flujoId, GuardarReglaRequest request, AppDbContext db, TiposDeConexion tipos, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Proceso no encontrado.");
        if (await ComprobarAsync(flujoId, request, db, tipos, ct) is { } incorrecta) return incorrecta;

        var ahora = DateTimeOffset.UtcNow;
        var regla = new ReglaDeNotificacion { FlujoId = flujoId, Nombre = string.Empty, Asunto = string.Empty, Cuerpo = string.Empty, CreatedAt = ahora };
        Aplicar(regla, request, ahora);
        db.Add(regla);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ADto(regla, await NombresAsync(db, flujoId, ct)));
    }

    private static async Task<IResult> ActualizarAsync(Guid flujoId, Guid id, GuardarReglaRequest request, AppDbContext db, TiposDeConexion tipos, HttpContext http, CancellationToken ct)
    {
        var regla = await db.Set<ReglaDeNotificacion>().FirstOrDefaultAsync(r => r.Id == id && r.FlujoId == flujoId, ct);
        if (regla is null) return ProblemResults.NotFound(http, "Regla no encontrada.");
        if (await ComprobarAsync(flujoId, request, db, tipos, ct) is { } incorrecta) return incorrecta;

        Aplicar(regla, request, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ADto(regla, await NombresAsync(db, flujoId, ct)));
    }

    private static async Task<IResult> BorrarAsync(Guid flujoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var borradas = await db.Set<ReglaDeNotificacion>().Where(r => r.Id == id && r.FlujoId == flujoId).ExecuteDeleteAsync(ct);
        return borradas == 0 ? ProblemResults.NotFound(http, "Regla no encontrada.") : Results.NoContent();
    }

    /// <summary>Sends the rule's notice now, written with the process's latest case (or with example values if it has none), to the
    /// address given or to the rule's recipients. Straight through the account, not the outbox: the answer says whether it went out.</summary>
    private static async Task<IResult> ProbarAsync(
        Guid flujoId, Guid id, ProbarReglaRequest? request, AppDbContext db, EnviadorDeMensajes enviador, IOptions<NotificacionesOptions> opciones,
        HttpContext http, CancellationToken ct)
    {
        var regla = await db.Set<ReglaDeNotificacion>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.FlujoId == flujoId, ct);
        if (regla is null) return ProblemResults.NotFound(http, "Regla no encontrada.");

        IReadOnlyList<string> destinatarios = string.IsNullOrWhiteSpace(request?.Destinatario) ? regla.Destinatarios : [request.Destinatario.Trim()];
        if (destinatarios.Count == 0 || destinatarios.Any(d => !MailAddress.TryCreate(d, out _))) return ProblemResults.Conflict(http, "Indica una dirección válida para la prueba.");

        var proceso = await db.Set<Flujo>().AsNoTracking().Where(f => f.Id == flujoId).Select(f => f.Nombre).FirstAsync(ct);
        var ultimo = await db.Set<Caso>().AsNoTracking()
            .Where(c => c.FlujoId == flujoId && (regla.TipoCasoId == null || c.TipoCasoId == regla.TipoCasoId))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var contexto = new ContextoDelAviso(
            ultimo?.Id ?? Guid.Empty, ultimo?.Titulo ?? "Caso de ejemplo", proceso, null, regla.Codigo, DateTimeOffset.UtcNow, ultimo?.DatosJson,
            "{\"ejemplo\":true}");
        var (asunto, cuerpo) = RedaccionDeAvisos.Redactar(regla.Asunto, regla.Cuerpo, contexto, opciones.Value);

        try
        {
            await enviador.EnviarAsync(regla.ConexionId, new Mensaje(destinatarios, $"[Prueba] {asunto}", cuerpo), ct);
            return Results.Ok(new { Enviado = true, Destinatarios = destinatarios, Asunto = asunto, Cuerpo = cuerpo });
        }
        catch (ConexionException ex)
        {
            return Results.Ok(new { Enviado = false, Destinatarios = destinatarios, Asunto = asunto, Cuerpo = cuerpo, Error = ex.Message });
        }
    }

    private static async Task<IResult> EnviosAsync(Guid flujoId, string? estado, int? page, int? pageSize, AppDbContext db, CancellationToken ct)
    {
        var pagina = page is null or < 1 ? 1 : page.Value;
        var tamano = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        var consulta = db.Set<EnvioDeNotificacion>().AsNoTracking().Where(e => e.FlujoId == flujoId);
        if (Enum.TryParse<EstadoDeEnvio>(estado, true, out var filtro)) consulta = consulta.Where(e => e.Estado == filtro);

        var total = await consulta.CountAsync(ct);
        var envios = await consulta.OrderByDescending(e => e.CreatedAt).Skip((pagina - 1) * tamano).Take(tamano)
            .Join(db.Set<Caso>(), e => e.CasoId, c => c.Id, (e, c) => new { e, c.Titulo })
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<EnvioDeNotificacionDto>(
            envios.Select(x => new EnvioDeNotificacionDto(
                x.e.Id, x.e.ReglaId, x.e.ReglaNombre, x.e.CasoId, x.Titulo, x.e.Destinatarios, x.e.Asunto, x.e.Estado.ToString(), x.e.Intentos,
                x.e.UltimoError, x.e.CreatedAt, x.e.EnviadoAt)).ToList(),
            pagina, tamano, total));
    }

    /// <summary>A notice that failed every attempt goes back to the outbox, from zero.</summary>
    private static async Task<IResult> ReintentarAsync(Guid flujoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var envio = await db.Set<EnvioDeNotificacion>().FirstOrDefaultAsync(e => e.Id == id && e.FlujoId == flujoId, ct);
        if (envio is null) return ProblemResults.NotFound(http, "Envío no encontrado.");
        if (envio.Estado == EstadoDeEnvio.Enviado) return ProblemResults.Conflict(http, "Ese aviso ya se envió.");

        envio.Estado = EstadoDeEnvio.Pendiente;
        envio.Intentos = 0;
        envio.ProximoIntentoAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult?> ComprobarAsync(Guid flujoId, GuardarReglaRequest request, AppDbContext db, TiposDeConexion tipos, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        void Error(string campo, string mensaje) => errores[campo] = [.. errores.GetValueOrDefault(campo, []), mensaje];

        if (string.IsNullOrWhiteSpace(request.Nombre) || request.Nombre.Trim().Length > 100) Error("nombre", "El nombre es obligatorio, de 100 letras como mucho.");

        if (!Enum.TryParse<DisparadorDeNotificacion>(request.Disparador, true, out var disparador))
        {
            Error("disparador", $"Disparador desconocido. Los que hay: {string.Join(", ", Enum.GetNames<DisparadorDeNotificacion>())}.");
        }
        else if (disparador == DisparadorDeNotificacion.EventoDelRobot && (request.Codigo is null || !CodigoDeEvento().IsMatch(request.Codigo.Trim())))
        {
            Error("codigo", "Indica el evento del robot: letras minúsculas, números o _ (p. ej. precio_bajado).");
        }
        else if (disparador == DisparadorDeNotificacion.EstadoNegocio)
        {
            var codigo = request.Codigo?.Trim();
            if (string.IsNullOrEmpty(codigo) || !await db.Set<FlujoEstadoDef>().AnyAsync(e => e.FlujoId == flujoId && e.Codigo == codigo, ct))
            {
                Error("codigo", "Elige un estado de negocio del proceso.");
            }
        }

        if (request.TipoCasoId is { } tipoId && !await db.Set<FlujoTipoCasoDef>().AnyAsync(t => t.Id == tipoId && t.FlujoId == flujoId, ct))
        {
            Error("tipoCasoId", "Ese tipo de caso no es de este proceso.");
        }

        var conexion = await db.Set<Conexion>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ConexionId, ct);
        if (conexion is null || tipos.Buscar(conexion.Tipo) is not ITipoQueEnviaMensajes) Error("conexionId", "Elige una conexión que envíe mensajes (Configuración → Conexiones).");

        var destinatarios = (request.Destinatarios ?? []).Select(d => d?.Trim() ?? string.Empty).Where(d => d.Length > 0).ToList();
        if (destinatarios.Count is 0 or > 20) Error("destinatarios", "Indica entre 1 y 20 direcciones.");
        foreach (var malo in destinatarios.Where(d => !MailAddress.TryCreate(d, out _))) Error("destinatarios", $"«{malo}» no es una dirección de correo.");

        if (string.IsNullOrWhiteSpace(request.Asunto) || request.Asunto.Length > 300) Error("asunto", "El asunto es obligatorio, de 300 letras como mucho.");
        if (string.IsNullOrWhiteSpace(request.Cuerpo) || request.Cuerpo.Length > 5000) Error("cuerpo", "El texto es obligatorio, de 5000 letras como mucho.");
        foreach (var (campo, plantilla) in new[] { ("asunto", request.Asunto ?? string.Empty), ("cuerpo", request.Cuerpo ?? string.Empty) })
        {
            var desconocidos = RedaccionDeAvisos.Desconocidos(plantilla);
            if (desconocidos.Count > 0)
            {
                Error(campo, $"No sé qué poner en {string.Join(", ", desconocidos.Select(d => "{" + d + "}"))}. Se puede usar: " +
                    string.Join(", ", RedaccionDeAvisos.Variables.Select(v => "{" + v + "}")) + ", {datos.campo} y {evento.campo}.");
            }
        }

        return errores.Count == 0 ? null : Results.ValidationProblem(errores);
    }

    private static void Aplicar(ReglaDeNotificacion regla, GuardarReglaRequest request, DateTimeOffset ahora)
    {
        var disparador = Enum.Parse<DisparadorDeNotificacion>(request.Disparador, true);
        regla.Nombre = request.Nombre.Trim();
        regla.Activa = request.Activa;
        regla.Disparador = disparador;
        regla.Codigo = disparador is DisparadorDeNotificacion.EventoDelRobot or DisparadorDeNotificacion.EstadoNegocio ? request.Codigo!.Trim() : null;
        regla.TipoCasoId = request.TipoCasoId;
        regla.ConexionId = request.ConexionId;
        regla.Destinatarios = (request.Destinatarios ?? []).Select(d => d.Trim()).Where(d => d.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        regla.Asunto = request.Asunto.Trim();
        regla.Cuerpo = request.Cuerpo.Trim();
        regla.UpdatedAt = ahora;
    }

    private sealed record Nombres(IReadOnlyDictionary<Guid, string> Tipos, IReadOnlyDictionary<Guid, string> Conexiones);

    private static async Task<Nombres> NombresAsync(AppDbContext db, Guid flujoId, CancellationToken ct) => new(
        await db.Set<FlujoTipoCasoDef>().AsNoTracking().Where(t => t.FlujoId == flujoId).ToDictionaryAsync(t => t.Id, t => t.Nombre, ct),
        await db.Set<Conexion>().AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Nombre, ct));

    private static ReglaDeNotificacionDto ADto(ReglaDeNotificacion r, Nombres nombres) => new(
        r.Id, r.FlujoId, r.TipoCasoId, r.TipoCasoId is { } t ? nombres.Tipos.GetValueOrDefault(t) : null, r.Nombre, r.Activa, r.Disparador.ToString(), r.Codigo,
        r.ConexionId, nombres.Conexiones.GetValueOrDefault(r.ConexionId, "(borrada)"), r.Destinatarios, r.Asunto, r.Cuerpo, r.CreatedAt, r.UpdatedAt);
}
