using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class CreateFlujoTipoCasoRequestValidator : AbstractValidator<CreateFlujoTipoCasoRequest>
{
    public CreateFlujoTipoCasoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
    }
}
