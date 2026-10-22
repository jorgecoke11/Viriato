using FluentValidation;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Flujos.Validation;

public sealed class GuardarFormularioRequestValidator : AbstractValidator<GuardarFormularioRequest>
{
    public GuardarFormularioRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descripcion).MaximumLength(300);
        RuleFor(x => x.EsquemaJson).Custom((esquema, context) =>
        {
            foreach (var error in EsquemaDatos.Validar(esquema))
            {
                context.AddFailure("EsquemaJson", error);
            }
        });
    }
}
