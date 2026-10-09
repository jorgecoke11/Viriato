using FluentValidation;
using Viariato.Modules.Casos.Contracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class AccionMasivaRequestValidator : AbstractValidator<AccionMasivaRequest>
{
    /// <summary>One request is one transaction's worth of work: a bigger selection goes in several.</summary>
    public const int MaximoPorPeticion = 500;

    public AccionMasivaRequestValidator()
    {
        RuleFor(x => x.Ids).NotNull().NotEmpty().WithMessage("Indica al menos un caso.");
        RuleFor(x => x.Ids.Count).LessThanOrEqualTo(MaximoPorPeticion).When(x => x.Ids is not null)
            .WithMessage($"No se puede aplicar una acción a más de {MaximoPorPeticion} casos de una vez.");
    }
}
