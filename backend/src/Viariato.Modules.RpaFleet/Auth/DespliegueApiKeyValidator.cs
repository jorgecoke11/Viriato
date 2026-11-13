using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.RpaFleet.Lanzador;

namespace Viariato.Modules.RpaFleet.Auth;

/// <summary>Accepts a Despliegue's own key or a pass the launcher next to Viriato handed one of its robots
/// (<see cref="SecretoDelLanzador"/>): both authenticate as the Despliegue, and behave the same from then on.</summary>
public sealed class DespliegueApiKeyValidator(AppDbContext db, SecretoDelLanzador secreto) : IDespliegueApiKeyValidator
{
    public async Task<DespliegueIdentity?> ValidateAsync(string rawKey, CancellationToken ct, bool registrarUso = true)
    {
        Despliegue? despliegue;
        if (rawKey.StartsWith(Viariato.ApiContracts.RpaHeaders.PrefijoDePase, StringComparison.Ordinal))
        {
            if (secreto.ValidarPase(rawKey) is not { } id) return null;
            despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        }
        else
        {
            var hash = ApiKeyGenerator.Hash(rawKey);
            despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.ApiKeyHash == hash, ct);
        }

        if (despliegue is null)
        {
            return null;
        }

        if (registrarUso)
        {
            await db.Set<Despliegue>()
                .Where(d => d.Id == despliegue.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastUsedAt, DateTimeOffset.UtcNow), ct);
        }

        return new DespliegueIdentity(despliegue.Id, despliegue.EquipoId, despliegue.ServicioId, despliegue.FlujoId, despliegue.Encendido);
    }
}
