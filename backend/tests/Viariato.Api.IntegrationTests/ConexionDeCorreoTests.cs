using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Viariato.Modules.Conexiones.Tipos;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// A mail server that cannot be reached, or that breaks the secure connection, is something the person testing the account has to
/// read about ("Probar" in Conexiones), not a 500. A fake SMTP server on this machine plays each part.
/// </summary>
public sealed class ConexionDeCorreoTests
{
    private static readonly Mensaje Prueba = new(["destino@example.com"], "Prueba", "Hola");

    private static JsonElement Configuracion(int puerto) => JsonDocument.Parse(
        $$"""{"servidor":"127.0.0.1","puerto":{{puerto}},"usuario":"yo@example.com","remitente":"yo@example.com"}""").RootElement;

    /// <summary>Accepts one connection and hands it to <paramref name="atender"/>; returns the port it listens on.</summary>
    private static (int Puerto, Task Servidor) ServidorFalso(Func<NetworkStream, Task> atender)
    {
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        var servidor = Task.Run(async () =>
        {
            try
            {
                using var cliente = await escucha.AcceptTcpClientAsync();
                await atender(cliente.GetStream());
            }
            catch (IOException) { /* the client hung up: what the test expects */ }
            finally
            {
                escucha.Stop();
            }
        });
        return (puerto, servidor);
    }

    [Fact]
    public async Task ASecureConnectionThatCannotBeOpened_IsReportedAsAMessage()
    {
        // Offers STARTTLS, then answers the handshake with plain text: what a mismatched port or a meddling proxy looks like.
        var (puerto, servidor) = ServidorFalso(async red =>
        {
            using var lector = new StreamReader(red, Encoding.ASCII, leaveOpen: true);
            async Task Escribir(string texto) => await red.WriteAsync(Encoding.ASCII.GetBytes(texto));

            await Escribir("220 falso ESMTP\r\n");
            await lector.ReadLineAsync(); // EHLO
            await Escribir("250-falso\r\n250 STARTTLS\r\n");
            await lector.ReadLineAsync(); // STARTTLS
            await Escribir("220 adelante\r\n");
            await Escribir("esto no es TLS\r\n");
            await lector.ReadLineAsync();
        });

        var ex = await Assert.ThrowsAsync<ConexionException>(
            () => new ConexionDeCorreo().EnviarAsync(Configuracion(puerto), "clave", Prueba, CancellationToken.None));

        Assert.Contains("conexión segura", ex.Message);
        Assert.Contains($"127.0.0.1:{puerto}", ex.Message);
        await servidor;
    }

    [Fact]
    public async Task AServerThatNeverAnswers_IsReportedAsAMessage()
    {
        var (puerto, servidor) = ServidorFalso(async red => await Task.Delay(TimeSpan.FromSeconds(5)));

        var ex = await Assert.ThrowsAsync<ConexionException>(
            () => new ConexionDeCorreo { MilisegundosDeEspera = 300 }.EnviarAsync(Configuracion(puerto), "clave", Prueba, CancellationToken.None));

        Assert.Contains("no contesta", ex.Message);
        await servidor;
    }
}
