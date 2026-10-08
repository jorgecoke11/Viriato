using FluentValidation;
using Viariato.Modules.RpaFleet.Contracts;

namespace Viariato.Modules.RpaFleet.Validation;

public sealed class CreateServicioRequestValidator : AbstractValidator<CreateServicioRequest>
{
    public CreateServicioRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descripcion).MaximumLength(1000);
        RuleFor(x => x.MaxEjecucionesGlobales).InclusiveBetween(1, 1000).When(x => x.MaxEjecucionesGlobales is not null)
            .WithMessage("El límite global debe estar entre 1 y 1000.");
        RuleFor(x => x.TiempoMaximoMinutos).InclusiveBetween(1, 10_080).When(x => x.TiempoMaximoMinutos is not null)
            .WithMessage("El tiempo máximo debe estar entre 1 minuto y 7 días (10080 minutos).");
    }
}
