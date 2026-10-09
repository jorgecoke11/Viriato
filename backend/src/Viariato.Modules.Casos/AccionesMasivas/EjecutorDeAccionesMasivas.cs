using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Endpoints;

namespace Viariato.Modules.Casos.AccionesMasivas;

/// <summary>
/// Runs any <see cref="IAccionMasivaSobreCaso"/> over a selection of Casos. What is the same for every action lives here:
/// a Caso that does not exist, or that is in a process the caller is not assigned to, is reported as not found (so the answer
/// never confirms that a Caso exists); one the action does not apply to is left alone with the action's reason; and one that
/// fails does not stop the rest. The caller always gets the whole story: how many were done, and why each of the others was not.
/// </summary>
public sealed class EjecutorDeAccionesMasivas(AppDbContext db)
{
    public async Task<ResultadoAccionMasiva> EjecutarAsync(
        IAccionMasivaSobreCaso accion, IReadOnlyList<Guid> ids, JsonElement? parametros, Guid usuarioId, CancellationToken ct)
    {
        var unicos = ids.Distinct().ToList();
        var flujosAsignados = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, usuarioId, ct);
        var casos = await db.Set<Caso>().AsNoTracking()
            .Where(c => unicos.Contains(c.Id))
            .Select(c => new CasoParaAccion(c.Id, c.FlujoId, c.Estado))
            .ToDictionaryAsync(c => c.Id, ct);

        var procesados = 0;
        var omitidos = new List<ItemOmitidoDto>();
        foreach (var id in unicos)
        {
            if (!casos.TryGetValue(id, out var caso) || !flujosAsignados.Contains(caso.FlujoId))
            {
                omitidos.Add(new ItemOmitidoDto(id, "Caso no encontrado."));
                continue;
            }

            if (accion.MotivoPorElQueNoAplica(caso) is { } motivo)
            {
                omitidos.Add(new ItemOmitidoDto(id, motivo));
                continue;
            }

            try
            {
                await accion.EjecutarAsync(caso, parametros, ct);
                procesados++;
            }
            catch (InvalidOperationException ex)
            {
                omitidos.Add(new ItemOmitidoDto(id, ex.Message));
            }
        }

        return new ResultadoAccionMasiva(procesados, omitidos);
    }
}
