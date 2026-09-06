using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class UpdateFlujoRequestValidator : AbstractValidator<UpdateFlujoRequest>
{
    public UpdateFlujoRequestValidator()
    {
        RuleFor(x => x.Nombre).MaximumLength(200).When(x => x.Nombre is not null);
        RuleFor(x => x.Descripcion).MaximumLength(1000).When(x => x.Descripcion is not null);
    }
}
