using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class CreateFlujoVersionRequestValidator : AbstractValidator<CreateFlujoVersionRequest>
{
    public CreateFlujoVersionRequestValidator()
    {
        RuleFor(x => x.Notas).MaximumLength(500);
    }
}
