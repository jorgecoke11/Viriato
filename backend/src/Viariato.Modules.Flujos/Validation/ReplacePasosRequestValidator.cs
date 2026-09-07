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
