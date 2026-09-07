namespace Viariato.Modules.Casos.Storage;

/// <summary>Resolves the right IDocumentStorage for a given Flujo — each one can point at its own
/// storage backend/credentials, so this can't be a single process-wide instance.</summary>
public interface IDocumentStorageResolver
{
    Task<IDocumentStorage> ResolveForFlujoAsync(Guid flujoId, CancellationToken ct);
}
