using MailAddress = System.Net.Mail.MailAddress;
using System.Text.Json;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Viariato.Modules.Conexiones.Tipos;

/// <summary>
/// A mailbox, by SMTP. With Gmail: server smtp.gmail.com, port 587, the address as user and an "app password" (Google account →
/// Security → App passwords) as the secret — the normal password does not work there.
/// </summary>
internal sealed class ConexionDeCorreo : ITipoQueEnviaMensajes
{
    public string Codigo => "correo";

    public string Nombre => "Correo (SMTP)";

    public string? EtiquetaSecreto => "Contraseña (en Gmail, una contraseña de aplicación)";

    public IReadOnlyList<CampoDeConexion> Campos { get; } =
    [
        new("servidor", "Servidor SMTP", "texto", true, "smtp.gmail.com"),
        new("puerto", "Puerto", "numero", true, 587, "587 (STARTTLS) o 465 (SSL)."),
        new("usuario", "Usuario", "texto", true, null, "Normalmente, la dirección de correo."),
        new("remitente", "Remitente", "texto", true, null, "La dirección desde la que salen los correos."),
        new("nombreRemitente", "Nombre del remitente", "texto", false, "Viriato"),
    ];

    public IReadOnlyList<string> Validar(JsonElement configuracion)
    {
        var errores = new List<string>();
        if (Texto(configuracion, "remitente") is { } remitente && !MailAddress.TryCreate(remitente, out _))
        {
            errores.Add("El remitente no es una dirección de correo válida.");
        }
        if (configuracion.TryGetProperty("puerto", out var puerto) && puerto.ValueKind == JsonValueKind.Number &&
            (!puerto.TryGetInt32(out var numero) || numero is < 1 or > 65535))
        {
            errores.Add("El puerto tiene que estar entre 1 y 65535.");
        }
        return errores;
    }

    public async Task EnviarAsync(JsonElement configuracion, string? secreto, Mensaje mensaje, CancellationToken ct)
    {
        var servidor = Texto(configuracion, "servidor")!;
        var puerto = configuracion.GetProperty("puerto").GetInt32();

        var correo = new MimeMessage();
        correo.From.Add(new MailboxAddress(Texto(configuracion, "nombreRemitente") ?? "Viriato", Texto(configuracion, "remitente")));
        foreach (var destinatario in mensaje.Destinatarios) correo.To.Add(MailboxAddress.Parse(destinatario));
        correo.Subject = mensaje.Asunto;
        correo.Body = new TextPart("plain") { Text = mensaje.Cuerpo };

        using var cliente = new SmtpClient { Timeout = 30_000 };
        try
        {
            await cliente.ConnectAsync(servidor, puerto, puerto == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
            if (!string.IsNullOrEmpty(secreto)) await cliente.AuthenticateAsync(Texto(configuracion, "usuario"), secreto, ct);
            await cliente.SendAsync(correo, ct);
            await cliente.DisconnectAsync(true, ct);
        }
        catch (AuthenticationException ex)
        {
            throw new ConexionException("El servidor de correo no acepta el usuario o la contraseña (en Gmail hace falta una contraseña de aplicación).", ex);
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or ServiceNotConnectedException or IOException or System.Net.Sockets.SocketException)
        {
            throw new ConexionException($"No se pudo enviar el correo por {servidor}:{puerto}: {ex.Message}", ex);
        }
    }

    private static string? Texto(JsonElement configuracion, string campo) =>
        configuracion.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()!.Trim()
            : null;
}
