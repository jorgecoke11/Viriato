using System.Text.Json;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Shared.Authorization;

namespace Viariato.Modules.Casos.AccionesMasivas;

/// <summary>Cancels the Caso exactly as if a person had cancelled it from its own page.</summary>
public sealed class CancelarCasoAccion(IEjecucionOrchestrator orchestrator) : IAccionMasivaSobreCaso
{
    private static readonly CasoEstado[] Cancelables =
        [CasoEstado.Iniciado, CasoEstado.Pendiente, CasoEstado.EnProgreso, CasoEstado.Pausado, CasoEstado.EsperandoRevisionHumana];

    public string Id => "cancelar";

    public string Permiso => Permissions.CasosCancelar;

    public string? MotivoPorElQueNoAplica(CasoParaAccion caso) =>
        Cancelables.Contains(caso.Estado) ? null : $"Ya estaba {(caso.Estado == CasoEstado.Cancelado ? "cancelado" : "finalizado")}.";

    public Task EjecutarAsync(CasoParaAccion caso, JsonElement? parametros, CancellationToken ct) => orchestrator.CancelarAsync(caso.Id, ct);
}
