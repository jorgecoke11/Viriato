using FluentValidation;
using Viariato.Modules.RpaFleet.Contracts;

namespace Viariato.Modules.RpaFleet.Validation;

public sealed class CreateEquipoRequestValidator : AbstractValidator<CreateEquipoRequest>
{
    public CreateEquipoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descripcion).MaximumLength(1000);
    }
}
