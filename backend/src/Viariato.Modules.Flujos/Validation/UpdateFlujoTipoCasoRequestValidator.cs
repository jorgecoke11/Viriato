using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class UpdateFlujoTipoCasoRequestValidator : AbstractValidator<UpdateFlujoTipoCasoRequest>
{
    public UpdateFlujoTipoCasoRequestValidator()
    {
        RuleFor(x => x.Nombre).MaximumLength(100).When(x => x.Nombre is not null);
    }
}
