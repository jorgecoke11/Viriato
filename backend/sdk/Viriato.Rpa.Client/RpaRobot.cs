using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Viriato.Rpa.Client;

/// <summary>
/// Everything a robot's <c>Program.cs</c> would otherwise repeat: configuration, logging, the client and
/// the worker, Ctrl+C handling and the startup check. The robot only adds its own services and its handler:
/// <code>
/// var builder = RpaRobot.CreateBuilder(args);
/// builder.Services.AddSingleton&lt;MiServicio&gt;();
/// builder.AddRpaHandler&lt;MiHandler&gt;();
/// return await builder.RunRobotAsync();
/// </code>
/// Configuration comes from <c>appsettings.json</c> next to the executable, environment variables and
/// command-line arguments: section <c>Viriato</c> (<see cref="RpaClientOptions"/>) and <c>Worker</c>
/// (<see cref="RpaWorkerOptions"/>).
/// </summary>
public static class RpaRobot
{
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        // appsettings.json travels next to the executable: without this the host looks for it in
        // whatever folder the robot happened to be launched from.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

        builder.Services.AddViriatoRpaClient(
            o => builder.Configuration.GetSection("Viriato").Bind(o),
            w => builder.Configuration.GetSection("Worker").Bind(w));
        builder.Services.AddSingleton<RpaRobotEstado>();

        return builder;
    }

    /// <summary>Registers the robot's implementation and the service that feeds it from the queue.</summary>
    public static HostApplicationBuilder AddRpaHandler<THandler>(this HostApplicationBuilder builder)
        where THandler : class, IRpaStepHandler
    {
        builder.Services.AddSingleton<THandler>();
        builder.Services.AddSingleton<IRpaStepHandler>(sp => sp.GetRequiredService<THandler>());
        builder.Services.AddHostedService<RpaWorkerService>();
        return builder;
    }

    /// <summary>
    /// Checks the configuration, then runs until Ctrl+C (or <paramref name="ct"/>). Returns the process exit
    /// code: 1 when the configuration is incomplete — every problem is listed at once, before a browser or any
    /// application has been opened — or when Viriato rejects the API key; otherwise 0.
    /// </summary>
    /// <param name="validar">The robot's own configuration check; returns what is missing or wrong.</param>
    /// <param name="error">Where startup problems are written (the console's error stream by default).</param>
    public static async Task<int> RunRobotAsync(
        this HostApplicationBuilder builder,
        Func<IEnumerable<string>>? validar = null,
        TextWriter? error = null,
        CancellationToken ct = default)
    {
        var viriato = builder.Configuration.GetSection("Viriato").Get<RpaClientOptions>() ?? new RpaClientOptions();
        var problemas = viriato.Validar().Concat(validar?.Invoke() ?? []).ToList();

        if (problemas.Count > 0)
        {
            var salida = error ?? Console.Error;
            salida.WriteLine("Falta configuración para arrancar el robot:");
            foreach (var problema in problemas) salida.WriteLine($"  - {problema}");
            return 1;
        }

        using var host = builder.Build();
        var estado = host.Services.GetRequiredService<RpaRobotEstado>(); // RunAsync disposes the host: fetch it first
        await host.RunAsync(ct);
        return estado.CodigoSalida;
    }
}
