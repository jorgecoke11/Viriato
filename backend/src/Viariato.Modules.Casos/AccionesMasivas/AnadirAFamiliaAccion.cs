using System.Text.Json;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Familias;
using Viariato.Shared.Authorization;

namespace Viariato.Modules.Casos.AccionesMasivas;

/// <summary>Puts the selected cases in an open family of their process (<c>{ "familiaId": "…" }</c>).</summary>
public sealed class AnadirAFamiliaAccion(GestorDeFamilias familias, AppDbContext db) : IAccionMasivaSobreCaso
{
    public string Id => "anadir-a-familia";

    public string Permiso => Permissions.CasosCrear;

    public string? ValidarParametros(JsonElement? parametros) =>
        FamiliaId(parametros) is null ? "Falta la familia (familiaId)." : null;

    public string? MotivoPorElQueNoAplica(CasoParaAccion caso) => null;

    public async Task EjecutarAsync(CasoParaAccion caso, JsonElement? parametros, CancellationToken ct)
    {
        try
        {
            await familias.AnadirAsync(FamiliaId(parametros)!.Value, [caso.Id], caso.FlujoId, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (FamiliaNoValidaException ex)
        {
            db.ChangeTracker.Clear();
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static Guid? FamiliaId(JsonElement? parametros) =>
        parametros is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty("familiaId", out var id) && id.ValueKind == JsonValueKind.String
            && Guid.TryParse(id.GetString(), out var guid) ? guid : null;
}
