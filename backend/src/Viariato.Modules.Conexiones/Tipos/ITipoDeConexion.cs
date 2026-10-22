using System.Text.Json;

namespace Viariato.Modules.Conexiones.Tipos;

/// <summary>One setting of a kind of account, described so the screen can draw its field without knowing the kind.</summary>
/// <param name="Tipo">texto, numero or booleano.</param>
public sealed record CampoDeConexion(string Codigo, string Etiqueta, string Tipo, bool Obligatorio, object? PorDefecto = null, string? Ayuda = null);

/// <summary>
/// A kind of account (see <c>Conexion</c>). A new one — Telegram, an API… — is a class that implements this, registered in
/// DependencyInjection: the screens, the API and the notifications pick it up from the registry without changes.
/// </summary>
public interface ITipoDeConexion
{
    /// <summary>Stored on the account: never change it once accounts use it.</summary>
    string Codigo { get; }

    string Nombre { get; }

    IReadOnlyList<CampoDeConexion> Campos { get; }

    /// <summary>What the secret is called on screen ("Contraseña"), or null if this kind keeps none.</summary>
    string? EtiquetaSecreto { get; }

    /// <summary>The problems with these settings, in words for a person; empty if they are fine. The generic checks (required
    /// fields, numbers that are numbers) are done before, from <see cref="Campos"/>.</summary>
    IReadOnlyList<string> Validar(JsonElement configuracion);
}

/// <summary>A message: to whom, about what, and what it says (plain text; line breaks are kept).</summary>
public sealed record Mensaje(IReadOnlyList<string> Destinatarios, string Asunto, string Cuerpo);

/// <summary>A kind of account that can send messages: what notifications go out through.</summary>
public interface ITipoQueEnviaMensajes : ITipoDeConexion
{
    /// <exception cref="ConexionException">It could not be sent, with the reason in words for a person.</exception>
    Task EnviarAsync(JsonElement configuracion, string? secreto, Mensaje mensaje, CancellationToken ct);
}

/// <summary>Something went wrong talking to the outside world; the message says what, for a person.</summary>
public sealed class ConexionException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);
