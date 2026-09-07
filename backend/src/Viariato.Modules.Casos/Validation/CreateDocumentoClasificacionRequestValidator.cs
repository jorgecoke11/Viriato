using FluentValidation;
using Viariato.Modules.Casos.Contracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class CreateDocumentoClasificacionRequestValidator : AbstractValidator<CreateDocumentoClasificacionRequest>
{
    public CreateDocumentoClasificacionRequestValidator()
    {
        RuleFor(x => x.TipoDocumentoId).NotEmpty();
        RuleFor(x => x.PaginaDesde).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PaginaHasta).GreaterThanOrEqualTo(x => x.PaginaDesde)
            .WithMessage("La página final no puede ser anterior a la inicial.");
    }
}
