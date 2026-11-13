using Microsoft.Extensions.Options;

namespace Viriato.Rpa.Lanzador;

/// <summary>What the launcher works with in one round: its settings and robots, each robot's key, and what is wrong, if anything.</summary>
/// <param name="Claves">The key each robot asks with and is handed, by robot name.</param>
/// <param name="Problemas">Empty means usable. With problems nothing new is started.</param>
/// <param name="ProblemasYaContados">The source already said them in the log, in its own words (they are not the catalogue being
/// wrong: Viriato still starting, say).</param>
public sealed record CatalogoLeido(
    LanzadorOptions Opciones, IReadOnlyDictionary<string, string> Claves, IReadOnlyList<string> Problemas, bool ProblemasYaContados = false);

/// <summary>Where the launcher's robots come from: a <c>robots.json</c> on disk (<see cref="CatalogoDeArchivo"/>) or Viriato itself
/// (<see cref="CatalogoDeViriato"/>), for the launcher that lives next to it.</summary>
public interface ICatalogoDelLanzador
{
    /// <param name="nadaEnMarcha">No copy of any robot is running: a good moment to tidy up what is no longer needed.</param>
    Task<CatalogoLeido> LeerAsync(bool nadaEnMarcha, CancellationToken ct);
}

/// <summary>The catalogue file (<c>robots.json</c>), read again when it changes; each robot's key is in the environment variable it names.</summary>
public sealed class CatalogoDeArchivo(IOptionsMonitor<LanzadorOptions> opciones, Func<string, string?> variable) : ICatalogoDelLanzador
{
    public Task<CatalogoLeido> LeerAsync(bool nadaEnMarcha, CancellationToken ct)
    {
        var o = opciones.CurrentValue;
        var problemas = o.Validar(variable);
        var claves = problemas.Count > 0
            ? new Dictionary<string, string>()
            : o.Robots.ToDictionary(r => r.Nombre, r => variable(r.ClaveEnv)!, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(new CatalogoLeido(o, claves, problemas));
    }
}
