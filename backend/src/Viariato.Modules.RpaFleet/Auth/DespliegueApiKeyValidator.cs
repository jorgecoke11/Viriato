using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Auth;

public sealed class DespliegueApiKeyValidator(AppDbContext db) : IDespliegueApiKeyValidator
{
    public async Task<DespliegueIdentity?> ValidateAsync(string rawKey, CancellationToken ct)
    {
        var hash = ApiKeyGenerator.Hash(rawKey);
        var despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.ApiKeyHash == hash, ct);
        if (despliegue is null)
        {
            return null;
        }

        await db.Set<Despliegue>()
            .Where(d => d.Id == despliegue.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastUsedAt, DateTimeOffset.UtcNow), ct);

        return new DespliegueIdentity(despliegue.Id, despliegue.EquipoId, despliegue.ServicioId, despliegue.FlujoId, despliegue.Encendido);
    }
}
