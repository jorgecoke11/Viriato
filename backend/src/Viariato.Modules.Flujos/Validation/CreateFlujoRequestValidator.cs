using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class CreateFlujoRequestValidator : AbstractValidator<CreateFlujoRequest>
{
    public CreateFlujoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descripcion).MaximumLength(1000);
    }
}
