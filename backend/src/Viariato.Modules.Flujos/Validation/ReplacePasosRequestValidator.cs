using System.Text.Json;
using FluentValidation;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Validation;

/// <summary>Validates a full step-list replacement: Orden must be unique and contiguous from 1, an
/// Agente step must name an AgenteDefinicion, and a Decision step's ordenSiVerdadero/ordenSiFalso
/// must point at Orden values that actually exist in the same list.</summary>
public sealed class ReplacePasosRequestValidator : AbstractValidator<ReplacePasosRequest>
{
    public ReplacePasosRequestValidator()
    {
        RuleFor(x => x.Pasos).NotEmpty();

        RuleForEach(x => x.Pasos).ChildRules(paso =>
        {
            paso.RuleFor(p => p.Nombre).NotEmpty().MaximumLength(200);
            paso.RuleFor(p => p.Orden).GreaterThan(0);
            paso.RuleFor(p => p.TipoPaso)
                .Must(t => Enum.TryParse<TipoPaso>(t, true, out _))
                .WithMessage("TipoPaso inválido.");
            paso.RuleFor(p => p.AgenteDefinicionId)
                .NotNull()
                .When(p => string.Equals(p.TipoPaso, nameof(TipoPaso.Agente), StringComparison.OrdinalIgnoreCase))
                .WithMessage("Un paso de tipo Agente requiere AgenteDefinicionId.");
            paso.RuleFor(p => p.ServicioId)
                .NotNull()
                .When(p => string.Equals(p.TipoPaso, nameof(TipoPaso.Rpa), StringComparison.OrdinalIgnoreCase))
                .WithMessage("Un paso de tipo Rpa requiere ServicioId.");
        });

        RuleFor(x => x.Pasos)
            .Must(pasos => pasos.Select(p => p.Orden).Distinct().Count() == pasos.Count)
            .WithMessage("Los valores de Orden deben ser únicos.");

        RuleFor(x => x.Pasos)
            .Must(TieneOrdenContiguo)
            .WithMessage("Los valores de Orden deben ser consecutivos empezando en 1.");

        RuleFor(x => x.Pasos)
            .Must(ReferenciasDeDecisionSonValidas)
            .WithMessage("Un paso de tipo Decision debe referenciar valores de Orden existentes en ordenSiVerdadero/ordenSiFalso.");

        RuleForEach(x => x.Pasos).ChildRules(paso =>
            paso.RuleFor(p => p.MaximoDeVisitas).GreaterThan(0).When(p => p.MaximoDeVisitas is not null)
                .WithMessage("El máximo de vueltas debe ser mayor que 0."));

        // The arrow-shaped flow: steps are nodes the arrows name by Clave.
        When(x => x.Transiciones is not null, () =>
        {
            RuleFor(x => x).Custom((request, context) =>
            {
                foreach (var error in ErroresDelGrafo(request.Pasos, request.Transiciones!))
                {
                    context.AddFailure("Transiciones", error);
                }
            });
        });
    }

    private static IEnumerable<string> ErroresDelGrafo(IReadOnlyList<FlujoPasoDefInput> pasos, IReadOnlyList<FlujoTransicionInput> transiciones)
    {
        var claves = pasos.Select(p => p.Clave?.Trim()).ToList();
        if (claves.Any(string.IsNullOrEmpty))
        {
            yield return "Con transiciones, cada paso necesita una clave.";
            yield break;
        }

        if (claves.Distinct(StringComparer.Ordinal).Count() != claves.Count)
        {
            yield return "Las claves de los pasos deben ser únicas.";
        }

        if (pasos.Any(p => string.Equals(p.TipoPaso, nameof(TipoPaso.Decision), StringComparison.OrdinalIgnoreCase)))
        {
            yield return "Con transiciones no hay pasos de tipo Decision: usa flechas con estados de negocio distintos.";
        }

        var serviciosRepetidos = pasos.Where(p => p.ServicioId is not null).GroupBy(p => p.ServicioId).Where(g => g.Count() > 1).ToList();
        if (serviciosRepetidos.Count > 0)
        {
            yield return "Cada servicio puede aparecer una sola vez en el flujo.";
        }

        var conocidas = claves.ToHashSet(StringComparer.Ordinal)!;
        var inicios = transiciones.Count(t => t.Origen is null);
        if (inicios != 1)
        {
            yield return "Debe haber exactamente una flecha desde Inicio.";
        }

        foreach (var t in transiciones)
        {
            if (t.Origen is not null && !conocidas.Contains(t.Origen))
            {
                yield return $"La flecha sale de un paso que no existe ({t.Origen}).";
            }

            if (!conocidas.Contains(t.Destino))
            {
                yield return $"La flecha llega a un paso que no existe ({t.Destino}).";
            }

            if (t.Origen is null)
            {
                if (t.Resultado is not null || t.EstadoNegocioCodigo is not null)
                {
                    yield return "La flecha desde Inicio no lleva resultado ni estado de negocio.";
                }
            }
            else if (!Enum.TryParse<ResultadoEjecucion>(t.Resultado, true, out _))
            {
                yield return "Cada flecha indica si la ejecución terminó Completada o Fallida.";
            }

            if (t.EstadoNegocioCodigo is { } codigo && !EstadosDeNegocio.EsCodigoValido(codigo.Trim()))
            {
                yield return $"\"{codigo}\" no es un código de estado válido (letras, números, \"_\" y \"-\", hasta 50).";
            }
        }

        var repetidas = transiciones
            .Where(t => t.Origen is not null)
            .GroupBy(t => (t.Origen, Resultado: t.Resultado?.Trim().ToLowerInvariant(), Estado: t.EstadoNegocioCodigo?.Trim()))
            .Where(g => g.Count() > 1);
        foreach (var grupo in repetidas)
        {
            yield return $"Hay dos flechas que salen del mismo paso con el mismo resultado y estado ({grupo.Key.Estado ?? "cualquier estado"}).";
        }
    }

    private static bool TieneOrdenContiguo(IReadOnlyList<FlujoPasoDefInput> pasos)
    {
        var ordenados = pasos.Select(p => p.Orden).OrderBy(o => o).ToList();
        return ordenados.SequenceEqual(Enumerable.Range(1, ordenados.Count));
    }

    private static bool ReferenciasDeDecisionSonValidas(IReadOnlyList<FlujoPasoDefInput> pasos)
    {
        var ordenesValidos = pasos.Select(p => p.Orden).ToHashSet();

        foreach (var paso in pasos)
        {
            if (!string.Equals(paso.TipoPaso, nameof(TipoPaso.Decision), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(paso.ConfiguracionJson))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(paso.ConfiguracionJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("ordenSiVerdadero", out var siVerdadero) ||
                    !root.TryGetProperty("ordenSiFalso", out var siFalso))
                {
                    return false;
                }

                if (!ordenesValidos.Contains(siVerdadero.GetInt32()) || !ordenesValidos.Contains(siFalso.GetInt32()))
                {
                    return false;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        return true;
    }
}
