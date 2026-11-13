using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Options;
using Viariato.Modules.RpaFleet.Lanzador;

namespace Viariato.Modules.RpaFleet.Tests;

/// <summary>The two things that let a robot be deployed by uploading a zip: working out how to run it, and the passes.</summary>
public sealed class LanzadorJuntoAViriatoTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "lanzador-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    private static MemoryStream Zip(params (string Nombre, string Contenido)[] archivos)
    {
        var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nombre, contenido) in archivos)
            {
                using var escritor = new StreamWriter(zip.CreateEntry(nombre).Open(), Encoding.UTF8);
                escritor.Write(contenido);
            }
        }

        memoria.Position = 0;
        return memoria;
    }

    private static ComoSeEjecuta Bien(MemoryStream zip)
    {
        var (como, problema) = PaqueteDeRobot.Analizar(zip);
        Assert.Null(problema);
        return como!;
    }

    private static string Mal(MemoryStream zip)
    {
        var (como, problema) = PaqueteDeRobot.Analizar(zip);
        Assert.Null(como);
        return problema!;
    }

    [Fact]
    public void ADotnetPublish_RunsItsProgram_WithoutAScreen()
    {
        var como = Bien(Zip(("Bsh.CasesCreator.dll", "x"), ("Bsh.CasesCreator.runtimeconfig.json", "{}"), ("appsettings.json", "{}")));

        Assert.Equal("", como.Raiz);
        Assert.Equal("dotnet", como.Comando);
        Assert.Equal(["Bsh.CasesCreator.dll"], como.Argumentos);
        Assert.False(como.Pantalla);
        Assert.Null(como.MemoriaMaximaMb);
    }

    [Fact]
    public void ARobotThatCarriesSelenium_GetsAScreen_AndAMemoryCeiling()
    {
        var como = Bien(Zip(("Bsh.PricesExtractor.dll", "x"), ("Bsh.PricesExtractor.runtimeconfig.json", "{}"), ("WebDriver.dll", "x")));

        Assert.True(como.Pantalla);
        Assert.Equal(PaqueteDeRobot.MemoriaPorDefectoConPantallaMb, como.MemoriaMaximaMb);
    }

    [Fact]
    public void AZipOfTheFolder_RunsFromInsideIt()
    {
        var como = Bien(Zip(("publicado/bsh-extractor/Robot.dll", "x"), ("publicado/bsh-extractor/Robot.runtimeconfig.json", "{}"),
            ("publicado/bsh-extractor/runtimes/linux/native/selenium-manager", "x")));

        Assert.Equal("publicado/bsh-extractor/", como.Raiz);
        Assert.Equal(["Robot.dll"], como.Argumentos);
    }

    [Fact]
    public void ARobotJson_SaysWhatCannotBeGuessed()
    {
        var como = Bien(Zip(
            ("run.sh", "#!/bin/sh"),
            ("robot.json", """{ "comando": "./run.sh", "argumentos": ["--rapido"], "pantalla": true, "memoriaMaximaMb": 900, "entorno": { "Portal__Url": "https://x" } }""")));

        Assert.Equal("./run.sh", como.Comando);
        Assert.Equal(["--rapido"], como.Argumentos);
        Assert.True(como.Pantalla);
        Assert.Equal(900, como.MemoriaMaximaMb);
        Assert.Equal("https://x", como.Entorno["Portal__Url"]);
    }

    [Fact]
    public void WhatTheRobotJsonDoesNotKnow_IsIgnored_ReplicasAreTheDespliegues()
    {
        var como = Bien(Zip(("A.dll", "x"), ("A.runtimeconfig.json", "{}"), ("robot.json", """{ "maxCopias": 3, "pantalla": true }""")));
        Assert.True(como.Pantalla);
    }

    [Fact]
    public void WhatCannotBeRun_IsRefused_WithAReasonForWhoeverUploadedIt()
    {
        Assert.Contains("no es un archivo .zip", Mal(new MemoryStream("hola"u8.ToArray())), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet publish", Mal(Zip(("leeme.txt", "x"))));
        Assert.Contains("varios programas", Mal(Zip(("A.dll", "x"), ("A.runtimeconfig.json", "{}"), ("B.dll", "x"), ("B.runtimeconfig.json", "{}"))));
        Assert.Contains("Falta A.dll", Mal(Zip(("A.runtimeconfig.json", "{}"))));
        Assert.Contains("ruta no permitida", Mal(Zip(("../fuera.dll", "x"), ("A.runtimeconfig.json", "{}"))));
        Assert.Contains("no se entiende", Mal(Zip(("robot.json", """{ "comando": """))));
    }

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;

        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private SecretoDelLanzador Secreto(Reloj reloj, string archivo = "secreto") =>
        new(Microsoft.Extensions.Options.Options.Create(new LanzadorOptions { ArchivoSecreto = Path.Combine(_carpeta, archivo), DiasDelPase = 7 }), reloj);

    [Fact]
    public void TheSecret_IsCreatedOnce_AndReadBackTheSame()
    {
        var reloj = new Reloj(DateTimeOffset.UtcNow);
        var primero = Secreto(reloj).Secreto();
        Assert.True(File.Exists(Path.Combine(_carpeta, "secreto")));
        Assert.Equal(primero, Secreto(reloj).Secreto());
        Assert.True(Secreto(reloj).Coincide(Encoding.UTF8.GetString(primero)));
        Assert.False(Secreto(reloj).Coincide("otro"));
        Assert.False(Secreto(reloj).Coincide(null));
    }

    [Fact]
    public void APass_IsTheDespliegues_UntilItExpires()
    {
        var reloj = new Reloj(DateTimeOffset.UtcNow);
        var secreto = Secreto(reloj);
        var despliegue = Guid.NewGuid();
        var pase = secreto.EmitirPase(despliegue);

        Assert.StartsWith("rpl_", pase);
        Assert.Equal(despliegue, secreto.ValidarPase(pase));

        reloj.Ahora = reloj.Ahora.AddDays(7).AddMinutes(1);
        Assert.Null(secreto.ValidarPase(pase));
    }

    [Fact]
    public void EveryPass_IsValid_WhateverItsSignatureLooksLike()
    {
        // Whatever the id and the signature look like, a pass made here is read back here.
        var secreto = Secreto(new Reloj(DateTimeOffset.UtcNow));
        for (var i = 0; i < 200; i++)
        {
            var despliegue = Guid.NewGuid();
            Assert.Equal(despliegue, secreto.ValidarPase(secreto.EmitirPase(despliegue)));
        }
    }

    [Fact]
    public void APass_CannotBeForged_OrUsedWithAnotherSecret()
    {
        var reloj = new Reloj(DateTimeOffset.UtcNow);
        var secreto = Secreto(reloj);
        var pase = secreto.EmitirPase(Guid.NewGuid());

        // Another Despliegue's id, or a later expiry, with the same signature.
        var partes = pase["rpl_".Length..].Split('.');
        Assert.Null(secreto.ValidarPase($"rpl_{Guid.NewGuid():N}.{partes[1]}.{partes[2]}"));
        Assert.Null(secreto.ValidarPase($"rpl_{partes[0]}.{long.Parse(partes[1]) + 999}.{partes[2]}"));
        // Without its prefix it is not a robot's pass.
        Assert.Null(secreto.ValidarPase(pase["rpl_".Length..]));
        Assert.Null(secreto.ValidarPase("rpl_basura"));
        Assert.Null(secreto.ValidarPase("rpa_una-clave-normal"));

        Assert.Null(Secreto(reloj, "otro-secreto").ValidarPase(pase));
    }

    [Fact]
    public void APassMadeForAnotherPurpose_IsNoRobotsPass_EvenWithTheSameSecret()
    {
        var reloj = new Reloj(DateTimeOffset.UtcNow);
        var secreto = Secreto(reloj);
        var deVista = Viariato.Shared.Seguridad.PaseFirmado.Emitir(
            secreto.Secreto(), "viriato:vista-en-directo", Guid.NewGuid(), reloj.Ahora.AddDays(1), "rpl_");

        Assert.Null(secreto.ValidarPase(deVista));
    }

    [Fact]
    public void WithoutALauncher_NoPassIsValid()
    {
        var secreto = new SecretoDelLanzador(Microsoft.Extensions.Options.Options.Create(new LanzadorOptions()), TimeProvider.System);
        Assert.False(secreto.Disponible);
        Assert.False(secreto.Coincide("x"));
        Assert.Null(secreto.ValidarPase("rpl_x.1.y"));
    }
}
