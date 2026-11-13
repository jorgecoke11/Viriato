using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Viariato.Modules.RpaFleet.Lanzador;

/// <summary>How to run a robot, worked out from its zip.</summary>
public sealed record ComoSeEjecuta(
    string Raiz, string Comando, IReadOnlyList<string> Argumentos, bool Pantalla, int? MemoriaMaximaMb,
    IReadOnlyDictionary<string, string> Entorno);

/// <summary>
/// Reads a robot's zip (the output of <c>dotnet publish</c>, zipped as it is or inside its folder) and works out how to run it, so
/// nobody has to describe it:
/// <list type="bullet">
/// <item>The program is the one <c>*.runtimeconfig.json</c> at its root: <c>dotnet Name.dll</c>.</item>
/// <item>It drives a browser if it carries Selenium or Playwright: it gets a virtual screen and a memory ceiling.</item>
/// </list>
/// A robot that is not like that (another language, several programs, settings of its own) says so in a <c>robot.json</c> at the
/// root of the zip: <c>{"comando", "argumentos", "pantalla", "memoriaMaximaMb", "entorno"}</c>, every field optional. How many replicas
/// run is not the robot's business: it is set on its Despliegue.
/// </summary>
public static class PaqueteDeRobot
{
    public const string Manifiesto = "robot.json";

    /// <summary>The ceiling of a copy with a browser when the robot does not say otherwise: Edge plus the robot, with room.</summary>
    public const int MemoriaPorDefectoConPantallaMb = 2048;

    // Libraries that mean "this robot opens a browser".
    private static readonly string[] DeNavegador = ["Viriato.Rpa.Client.Selenium.dll", "WebDriver.dll", "Microsoft.Playwright.dll"];

    private sealed class RobotJson
    {
        public string? Comando { get; set; }
        public List<string>? Argumentos { get; set; }
        public bool? Pantalla { get; set; }
        public int? MemoriaMaximaMb { get; set; }
        public Dictionary<string, string>? Entorno { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>How to run it, or the reason it cannot be, in words for whoever uploaded it.</summary>
    public static (ComoSeEjecuta? Como, string? Problema) Analizar(Stream zip)
    {
        ZipArchive archivo;
        try
        {
            archivo = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return (null, "No es un archivo .zip válido.");
        }

        using (archivo)
        {
            var ficheros = archivo.Entries.Where(e => !e.FullName.EndsWith('/')).ToList();
            if (ficheros.Count == 0) return (null, "El .zip está vacío.");

            foreach (var entrada in ficheros)
            {
                var nombre = entrada.FullName.Replace('\\', '/');
                if (nombre.StartsWith('/') || nombre.Split('/').Contains("..") || nombre.Contains(':'))
                {
                    return (null, $"El .zip tiene una ruta no permitida: '{entrada.FullName}'.");
                }
            }

            var raiz = Raiz(ficheros.Select(e => e.FullName.Replace('\\', '/')).ToList());
            var enLaRaiz = ficheros
                .Select(e => (Entrada: e, Nombre: e.FullName.Replace('\\', '/')[raiz.Length..]))
                .Where(e => !e.Nombre.Contains('/'))
                .ToDictionary(e => e.Nombre, e => e.Entrada, StringComparer.OrdinalIgnoreCase);

            RobotJson manifiesto = new();
            if (enLaRaiz.TryGetValue(Manifiesto, out var entradaManifiesto))
            {
                try
                {
                    using var lector = entradaManifiesto.Open();
                    manifiesto = JsonSerializer.Deserialize<RobotJson>(lector, Json) ?? new RobotJson();
                }
                catch (JsonException ex)
                {
                    return (null, $"El {Manifiesto} del .zip no se entiende: {ex.Message}");
                }
            }

            string comando;
            List<string> argumentos;
            if (!string.IsNullOrWhiteSpace(manifiesto.Comando))
            {
                comando = manifiesto.Comando.Trim();
                argumentos = manifiesto.Argumentos ?? [];
            }
            else
            {
                var programas = enLaRaiz.Keys.Where(n => n.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase)).ToList();
                if (programas.Count == 0)
                {
                    return (null, "No encuentro el programa: el .zip debe ser la salida de «dotnet publish» (con su archivo .runtimeconfig.json), "
                        + $"o llevar un {Manifiesto} que diga cómo se ejecuta.");
                }

                if (programas.Count > 1)
                {
                    return (null, $"El .zip tiene varios programas ({string.Join(", ", programas)}): pon un {Manifiesto} que diga cuál se ejecuta.");
                }

                var dll = programas[0][..^".runtimeconfig.json".Length] + ".dll";
                if (!enLaRaiz.ContainsKey(dll)) return (null, $"Falta {dll} en el .zip.");
                comando = "dotnet";
                argumentos = [dll];
            }

            if (manifiesto.MemoriaMaximaMb is <= 0) return (null, $"«memoriaMaximaMb» del {Manifiesto} debe ser mayor que 0.");

            var pantalla = manifiesto.Pantalla ?? DeNavegador.Any(enLaRaiz.ContainsKey);
            var memoria = manifiesto.MemoriaMaximaMb ?? (pantalla ? MemoriaPorDefectoConPantallaMb : null);

            return (new ComoSeEjecuta(raiz, comando, argumentos, pantalla, memoria,
                manifiesto.Entorno ?? new Dictionary<string, string>()), null);
        }
    }

    // A zip of the folder ("bsh-extractor/…") rather than of what is in it: every file under one same folder, none at the top.
    private static string Raiz(List<string> nombres)
    {
        var raiz = string.Empty;
        while (true)
        {
            var primeras = nombres.Select(n => n[raiz.Length..]).ToList();
            if (primeras.Any(n => !n.Contains('/'))) return raiz;

            var carpetas = primeras.Select(n => n[..(n.IndexOf('/') + 1)]).Distinct(StringComparer.Ordinal).ToList();
            if (carpetas.Count != 1) return raiz;
            raiz += carpetas[0];
        }
    }
}
