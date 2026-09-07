using FluentValidation;
using Viariato.Modules.RpaFleet.Contracts;

namespace Viariato.Modules.RpaFleet.Validation;

public sealed class CreateDespliegueRequestValidator : AbstractValidator<CreateDespliegueRequest>
{
    public CreateDespliegueRequestValidator()
    {
        RuleFor(x => x.EquipoId).NotEmpty();
        RuleFor(x => x.ServicioId).NotEmpty();
        RuleFor(x => x.FlujoId).NotEmpty();
    }
}
