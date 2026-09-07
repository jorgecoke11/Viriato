using FluentValidation;
using Viariato.Modules.Casos.Contracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class UpdateDocumentoClasificacionRequestValidator : AbstractValidator<UpdateDocumentoClasificacionRequest>
{
    public UpdateDocumentoClasificacionRequestValidator()
    {
        RuleFor(x => x.TipoDocumentoId).NotEmpty().When(x => x.TipoDocumentoId is not null);
        RuleFor(x => x.PaginaDesde).GreaterThanOrEqualTo(1).When(x => x.PaginaDesde is not null);
        RuleFor(x => x.PaginaHasta).GreaterThanOrEqualTo(1).When(x => x.PaginaHasta is not null);
        RuleFor(x => x)
            .Must(x => x.PaginaHasta!.Value >= x.PaginaDesde!.Value)
            .WithName("PaginaHasta")
            .WithMessage("La página final no puede ser anterior a la inicial.")
            .When(x => x.PaginaDesde is not null && x.PaginaHasta is not null);
    }
}
