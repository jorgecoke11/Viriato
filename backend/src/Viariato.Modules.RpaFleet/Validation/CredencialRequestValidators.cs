using FluentValidation;
using Viariato.Modules.RpaFleet.Contracts;

namespace Viariato.Modules.RpaFleet.Validation;

public sealed class CreateCredencialRequestValidator : AbstractValidator<CreateCredencialRequest>
{
    public CreateCredencialRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9][a-z0-9._-]*$")
            .WithMessage("El nombre solo puede llevar minúsculas, números, punto, guion y guion bajo, y empezar por letra o número.");
        RuleFor(x => x.Descripcion).MaximumLength(1000);
        RuleFor(x => x.Usuario).MaximumLength(200);
        // Not trimmed: spaces can be part of a password.
        RuleFor(x => x.Password).NotEmpty().MaximumLength(1000);
    }
}

public sealed class UpdateCredencialRequestValidator : AbstractValidator<UpdateCredencialRequest>
{
    public UpdateCredencialRequestValidator()
    {
        RuleFor(x => x.Descripcion).MaximumLength(1000);
        RuleFor(x => x.Usuario).MaximumLength(200);
        RuleFor(x => x.Password).MaximumLength(1000);
    }
}
