using FluentValidation;
using Viariato.Modules.RpaFleet.Contracts;

namespace Viariato.Modules.RpaFleet.Validation;

public sealed class UpdateServicioRequestValidator : AbstractValidator<UpdateServicioRequest>
{
    public UpdateServicioRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200).When(x => x.Nombre is not null);
        RuleFor(x => x.Descripcion).MaximumLength(1000).When(x => x.Descripcion is not null);
    }
}
