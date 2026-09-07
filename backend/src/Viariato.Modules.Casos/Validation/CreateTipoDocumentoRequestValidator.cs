using FluentValidation;
using Viariato.Modules.Casos.Contracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class CreateTipoDocumentoRequestValidator : AbstractValidator<CreateTipoDocumentoRequest>
{
    public CreateTipoDocumentoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descripcion).MaximumLength(1000);
    }
}
