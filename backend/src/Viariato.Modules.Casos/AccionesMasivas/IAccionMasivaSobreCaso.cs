using System.Text.Json;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.AccionesMasivas;

/// <summary>What an action gets to see of a Caso: enough to decide whether it applies, never the whole entity.</summary>
public sealed record CasoParaAccion(Guid Id, Guid FlujoId, CasoEstado Estado);

/// <summary>
/// One thing that can be done to many Casos at once (cancel them, pause them, change their priority…). Adding an action is
/// writing a class that implements this and registering it in <c>DependencyInjection</c>: the endpoint
/// (<c>POST /api/v1/casos/acciones/{id}</c>), the checks that apply to every action — the Caso exists and the caller can see it,
/// the selection is not absurdly big, one Caso failing does not fail the lot — and the report of what was done and what was left
/// alone are the same for all of them, and are not touched.
/// </summary>
public interface IAccionMasivaSobreCaso
{
    /// <summary>What the URL calls it ("cancelar").</summary>
    string Id { get; }

    /// <summary>The permission the caller needs to run it, on top of being allowed to use the Casos API at all.</summary>
    string Permiso { get; }

    /// <summary>Whether the action can be applied to the given parameters; the reason if not (shown as a 400). Actions with no
    /// parameters need not say anything.</summary>
    string? ValidarParametros(JsonElement? parametros) => null;

    /// <summary>Why this Caso cannot receive the action ("Ya estaba cancelado."), or null if it can. A Caso that cannot is left
    /// alone and reported with this reason.</summary>
    string? MotivoPorElQueNoAplica(CasoParaAccion caso);

    /// <summary>Applies the action to one Caso. An <see cref="InvalidOperationException"/> leaves just this Caso out of the
    /// lot, with its message as the reason.</summary>
    Task EjecutarAsync(CasoParaAccion caso, JsonElement? parametros, CancellationToken ct);
}
