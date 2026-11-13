using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Viriato.Rpa.Lanzador;

// The launcher: one process, next to Viriato, that keeps every robot uploaded to Viriato running only while it has work.
// Nothing to configure: Viriato's docker-compose.prod.yml sets these.
//   LANZADOR_SECRETO   file with the secret shared with Viriato (Viriato creates it). Default: /secretos/lanzador
//   LANZADOR_ROBOTS    where downloaded robots are unpacked. Default: /var/lib/lanzador/robots
//   VIRIATO_URL        where Viriato's API is. Default: http://api:8080

string Variable(string nombre, string porDefecto) => Environment.GetEnvironmentVariable(nombre) is { Length: > 0 } valor ? valor : porDefecto;

var opciones = new LanzadorOptions { ViriatoUrl = Variable("VIRIATO_URL", "http://api:8080") };
var donde = new LanzadorJuntoAViriato(
    Path.GetFullPath(Variable("LANZADOR_SECRETO", "/secretos/lanzador")),
    Path.GetFullPath(Variable("LANZADOR_ROBOTS", "/var/lib/lanzador/robots")));
Directory.CreateDirectory(donde.CarpetaDeRobots);
var direccion = new Uri(opciones.ViriatoUrl.TrimEnd('/') + "/");

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(c =>
{
    c.SingleLine = true;
    c.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

builder.Services.AddSingleton(opciones);
builder.Services.AddSingleton(donde);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IArrancadorDeRobots, ArrancadorDeProcesos>();
// Resolved once, by the launcher, which keeps it: it remembers the last catalogue in case Viriato does not answer.
builder.Services.AddHttpClient<ICatalogoDelLanzador, CatalogoDeViriato>(http =>
{
    http.BaseAddress = direccion;
    // A robot with its browser libraries is tens of MB.
    http.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient<IViriatoDelLanzador, ViriatoDelLanzador>(http =>
{
    http.BaseAddress = direccion;
    http.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHostedService<LanzadorDeRobots>();
// Robots get their own grace to finish what they are doing; the host has to wait at least that long.
builder.Services.Configure<HostOptions>(h => h.ShutdownTimeout = TimeSpan.FromMinutes(2));

Console.Out.WriteLine($"Lanzador de Viriato ({opciones.ViriatoUrl}): los robots se suben en Viriato, en cada despliegue.");

using var host = builder.Build();
await host.RunAsync();
