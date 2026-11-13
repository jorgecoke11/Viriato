using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viriato.Rpa.Lanzador;

// The launcher: one process that keeps every robot running only while it has work in Viriato. Two ways to tell it which robots:
//
//  Next to Viriato (its docker-compose.prod.yml): the robots are uploaded in Viriato and the launcher takes them from there.
//   LANZADOR_SECRETO   file with the secret shared with Viriato (Viriato creates it; both containers mount it)
//   LANZADOR_ROBOTS    where downloaded robots are unpacked. Default: /var/lib/lanzador/robots
//   VIRIATO_URL        where Viriato's API is. Default: http://api:8080
//
//  On any other machine: a catalogue file.
//   LANZADOR_CATALOGO  path of the catalogue (robots.json). Default: ./robots.json
//   VIRIATO_URL        where Viriato is; wins over "viriatoUrl" in the catalogue
//   <claveEnv>         one variable per robot with the key of its Despliegue (named in the catalogue)

Func<string, string?> variable = Environment.GetEnvironmentVariable;
var secreto = variable("LANZADOR_SECRETO");
var juntoAViriato = !string.IsNullOrWhiteSpace(secreto);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(c =>
{
    c.SingleLine = true;
    c.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

if (juntoAViriato)
{
    var opciones = new LanzadorOptions { ViriatoUrl = variable("VIRIATO_URL") is { Length: > 0 } url ? url : "http://api:8080" };
    var donde = new LanzadorJuntoAViriato(
        Path.GetFullPath(secreto!),
        Path.GetFullPath(variable("LANZADOR_ROBOTS") is { Length: > 0 } carpeta ? carpeta : "/var/lib/lanzador/robots"));
    Directory.CreateDirectory(donde.CarpetaDeRobots);

    builder.Services.AddSingleton(opciones);
    builder.Services.AddSingleton(donde);
    // Resolved once, by the launcher, which keeps it: it remembers the last catalogue in case Viriato does not answer.
    builder.Services.AddHttpClient<ICatalogoDelLanzador, CatalogoDeViriato>(http =>
    {
        http.BaseAddress = new Uri(opciones.ViriatoUrl.TrimEnd('/') + "/");
        // A robot with its browser libraries is tens of MB.
        http.Timeout = TimeSpan.FromMinutes(5);
    });
    AddViriato(opciones.ViriatoUrl);
    Console.Out.WriteLine($"Lanzador junto a Viriato ({opciones.ViriatoUrl}): los robots se suben en Viriato, en cada despliegue.");
}
else
{
    var ruta = Path.GetFullPath(variable("LANZADOR_CATALOGO") is { Length: > 0 } indicada ? indicada : "robots.json");
    if (!File.Exists(ruta))
    {
        Console.Error.WriteLine($"No existe el catálogo de robots: {ruta}");
        Console.Error.WriteLine("Indica dónde está con la variable LANZADOR_CATALOGO (ver docs/lanzador-de-robots.md en el repositorio de Viriato).");
        return 1;
    }

    // Only the file: nothing in the environment can be mistaken for a setting. Read again when it changes, so adding a robot
    // does not need a restart.
    var catalogo = new ConfigurationBuilder().AddJsonFile(ruta, optional: false, reloadOnChange: true).Build();
    builder.Services.Configure<LanzadorOptions>(catalogo);
    builder.Services.PostConfigure<LanzadorOptions>(o =>
    {
        if (variable("VIRIATO_URL") is { Length: > 0 } url) o.ViriatoUrl = url;
    });
    builder.Services.AddSingleton<ICatalogoDelLanzador>(sp => new CatalogoDeArchivo(sp.GetRequiredService<IOptionsMonitor<LanzadorOptions>>(), variable));

    var leidas = new LanzadorOptions();
    catalogo.Bind(leidas);
    AddViriato(variable("VIRIATO_URL") is { Length: > 0 } url ? url : leidas.ViriatoUrl);
    Console.Out.WriteLine($"Catálogo: {ruta}");

    var problemas = leidas.Validar(variable);
    if (problemas.Count > 0)
    {
        // Not fatal: the catalogue is read again when it changes, and nothing starts until it is right.
        Console.Error.WriteLine("El catálogo tiene problemas:");
        foreach (var problema in problemas) Console.Error.WriteLine($"  - {problema}");
    }
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IArrancadorDeRobots>(new ArrancadorDeProcesos(variable));
builder.Services.AddHostedService(sp => new LanzadorDeRobots(
    sp.GetRequiredService<ICatalogoDelLanzador>(),
    sp.GetRequiredService<IViriatoDelLanzador>(),
    sp.GetRequiredService<IArrancadorDeRobots>(),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<ILogger<LanzadorDeRobots>>()));
// Robots get their own grace to finish what they are doing; the host has to wait at least that long.
builder.Services.Configure<HostOptions>(h => h.ShutdownTimeout = TimeSpan.FromMinutes(2));

using var host = builder.Build();
await host.RunAsync();
return 0;

void AddViriato(string viriatoUrl) =>
    builder.Services.AddHttpClient<IViriatoDelLanzador, ViriatoDelLanzador>(http =>
    {
        if (Uri.TryCreate(viriatoUrl.TrimEnd('/') + "/", UriKind.Absolute, out var direccion)) http.BaseAddress = direccion;
        http.Timeout = TimeSpan.FromSeconds(20);
    });
