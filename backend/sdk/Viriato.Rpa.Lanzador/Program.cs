using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Viriato.Rpa.Lanzador;

// The launcher: one process that keeps every robot of its catalogue running only while it has work in Viriato.
//   LANZADOR_CATALOGO  path of the catalogue (robots.json). Default: ./robots.json
//   VIRIATO_URL        where Viriato is; wins over "viriatoUrl" in the catalogue
//   <claveEnv>         one variable per robot with the key of its Despliegue (named in the catalogue)

var ruta = Path.GetFullPath(Environment.GetEnvironmentVariable("LANZADOR_CATALOGO") is { Length: > 0 } indicada ? indicada : "robots.json");
if (!File.Exists(ruta))
{
    Console.Error.WriteLine($"No existe el catálogo de robots: {ruta}");
    Console.Error.WriteLine("Indica dónde está con la variable LANZADOR_CATALOGO (ver docs/lanzador-de-robots.md en el repositorio de Viriato).");
    return 1;
}

// Only the file: nothing in the environment can be mistaken for a setting. Read again when it changes, so adding a robot
// does not need a restart.
var catalogo = new ConfigurationBuilder().AddJsonFile(ruta, optional: false, reloadOnChange: true).Build();
Func<string, string?> variable = Environment.GetEnvironmentVariable;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(c =>
{
    c.SingleLine = true;
    c.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

builder.Services.Configure<LanzadorOptions>(catalogo);
builder.Services.PostConfigure<LanzadorOptions>(o =>
{
    if (variable("VIRIATO_URL") is { Length: > 0 } url) o.ViriatoUrl = url;
});
builder.Services.AddSingleton(variable);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IArrancadorDeRobots>(new ArrancadorDeProcesos(variable));
builder.Services.AddHttpClient<IViriatoDelLanzador, ViriatoDelLanzador>((sp, http) =>
{
    var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<LanzadorOptions>>().CurrentValue;
    if (Uri.TryCreate(o.ViriatoUrl.TrimEnd('/') + "/", UriKind.Absolute, out var direccion)) http.BaseAddress = direccion;
    http.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHostedService<LanzadorDeRobots>();
// Robots get their own grace to finish what they are doing; the host has to wait at least that long.
builder.Services.Configure<HostOptions>(h => h.ShutdownTimeout = TimeSpan.FromMinutes(2));

Console.Out.WriteLine($"Catálogo: {ruta}");

using var host = builder.Build();
var problemas = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<LanzadorOptions>>().CurrentValue.Validar(variable);
if (problemas.Count > 0)
{
    // Not fatal: the catalogue is read again when it changes, and nothing starts until it is right.
    Console.Error.WriteLine("El catálogo tiene problemas:");
    foreach (var problema in problemas) Console.Error.WriteLine($"  - {problema}");
}

await host.RunAsync();
return 0;
