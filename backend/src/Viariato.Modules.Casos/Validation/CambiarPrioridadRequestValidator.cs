using FluentValidation;
using Viariato.Modules.Casos.Contracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class CambiarPrioridadRequestValidator : AbstractValidator<CambiarPrioridadRequest>
{
    public CambiarPrioridadRequestValidator()
    {
        RuleFor(x => x.Prioridad).InclusiveBetween(PrioridadDeEjecucion.Minima, PrioridadDeEjecucion.Maxima)
            .WithMessage($"La prioridad debe estar entre {PrioridadDeEjecucion.Minima} y {PrioridadDeEjecucion.Maxima}.");
    }
}

/// <summary>The range an execution's priority may take. Wide enough to leave room between levels, narrow enough that a typo
/// (an extra zero) is caught instead of silently putting an execution ahead of everything.</summary>
public static class PrioridadDeEjecucion
{
    public const int Minima = -1000;
    public const int Maxima = 1000;
}
