using FluentValidation;
using Viariato.Modules.Casos.Contracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class StartCasoRequestValidator : AbstractValidator<StartCasoRequest>
{
    public StartCasoRequestValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(300);
        RuleFor(x => x)
            .Must(x => x.FlujoId is not null || x.FlujoVersionId is not null)
            .WithMessage("Debes indicar FlujoId o FlujoVersionId.");
    }
}
