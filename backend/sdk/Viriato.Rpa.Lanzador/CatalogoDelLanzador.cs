namespace Viriato.Rpa.Lanzador;

/// <summary>What the launcher works with in one round: its settings and robots, each robot's key, and what is wrong, if anything.</summary>
/// <param name="Claves">The pass each robot asks with and is handed, by robot name.</param>
/// <param name="Problemas">Empty means usable. With problems nothing new is started (the source has already said why in the log).</param>
public sealed record CatalogoLeido(LanzadorOptions Opciones, IReadOnlyDictionary<string, string> Claves, IReadOnlyList<string> Problemas);

/// <summary>Where the launcher's robots come from: Viriato (<see cref="CatalogoDeViriato"/>). Apart so the launcher's rounds can be
/// tested without it.</summary>
public interface ICatalogoDelLanzador
{
    /// <param name="nadaEnMarcha">No copy of any robot is running: a good moment to tidy up what is no longer needed.</param>
    Task<CatalogoLeido> LeerAsync(bool nadaEnMarcha, CancellationToken ct);
}
