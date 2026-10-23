using Viariato.Shared.Texto;

namespace Viariato.Modules.Notificaciones.Envio;

public sealed class NotificacionesOptions
{
    public const string SectionName = "Notificaciones";

    /// <summary>How often the postman looks for notices to send.</summary>
    public int SegundosEntreRondas { get; set; } = 15;

    /// <summary>The clock {fecha} and {hora} are written in.</summary>
    public string ZonaHoraria { get; set; } = "Europe/Madrid";

    /// <summary>The address people open Viriato at (PUBLIC_URL), for {enlace}; without it {enlace} is empty.</summary>
    public string? UrlPublica { get; set; }
}

/// <summary>What a case looked like when a rule fired: the values a notice can be written with.</summary>
public sealed record ContextoDelAviso(
    Guid CasoId, string Caso, string Proceso, string? Tipo, string? Estado, DateTimeOffset Cuando, string? DatosJson, string? EventoJson);

/// <summary>Writes a rule's subject and body for one case (see ReglaDeNotificacion for the placeholders).</summary>
public static class RedaccionDeAvisos
{
    public static readonly string[] Variables = ["caso", "proceso", "tipo", "estado", "fecha", "hora", "enlace"];

    public static readonly string[] Documentos = ["datos", "evento"];

    public static IReadOnlyList<string> Desconocidos(string plantilla) => Plantilla.Desconocidos(plantilla, Variables, Documentos);

    public static (string Asunto, string Cuerpo) Redactar(string asunto, string cuerpo, ContextoDelAviso contexto, NotificacionesOptions opciones)
    {
        var zona = BuscarZona(opciones.ZonaHoraria);
        var local = TimeZoneInfo.ConvertTime(contexto.Cuando, zona);
        var valores = new Dictionary<string, string>
        {
            ["caso"] = contexto.Caso,
            ["proceso"] = contexto.Proceso,
            ["tipo"] = contexto.Tipo ?? string.Empty,
            ["estado"] = contexto.Estado ?? string.Empty,
            ["fecha"] = local.ToString("yyyy-MM-dd"),
            ["hora"] = local.ToString("HH:mm"),
            ["enlace"] = string.IsNullOrWhiteSpace(opciones.UrlPublica) ? string.Empty : $"{opciones.UrlPublica.TrimEnd('/')}/casos/{contexto.CasoId}",
        };
        var documentos = new Dictionary<string, string?> { ["datos"] = contexto.DatosJson, ["evento"] = contexto.EventoJson };

        // A subject is one line and has a ceiling; the body keeps its line breaks.
        var textoAsunto = string.Join(' ', Plantilla.Renderizar(asunto, valores, documentos).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (textoAsunto.Length > 300) textoAsunto = textoAsunto[..300];
        return (textoAsunto, Plantilla.Renderizar(cuerpo, valores, documentos).Trim());
    }

    private static TimeZoneInfo BuscarZona(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
