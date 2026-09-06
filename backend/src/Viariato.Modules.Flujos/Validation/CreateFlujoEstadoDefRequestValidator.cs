using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class CreateFlujoEstadoDefRequestValidator : AbstractValidator<CreateFlujoEstadoDefRequest>
{
    public CreateFlujoEstadoDefRequestValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("El código solo puede tener letras, números, guiones y guiones bajos.");
        RuleFor(x => x.Display).NotEmpty().MaximumLength(100);
    }
}
