using FluentValidation;
using Viariato.Modules.Casos.Contracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class UpdateTipoDocumentoRequestValidator : AbstractValidator<UpdateTipoDocumentoRequest>
{
    public UpdateTipoDocumentoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200).When(x => x.Nombre is not null);
        RuleFor(x => x.Descripcion).MaximumLength(1000).When(x => x.Descripcion is not null);
    }
}
