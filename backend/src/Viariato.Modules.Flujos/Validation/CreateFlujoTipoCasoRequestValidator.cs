using FluentValidation;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Flujos.Validation;

public sealed class CreateFlujoTipoCasoRequestValidator : AbstractValidator<CreateFlujoTipoCasoRequest>
{
    public CreateFlujoTipoCasoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EsquemaDatosJson).Custom((esquema, context) =>
        {
            if (string.IsNullOrWhiteSpace(esquema)) return;
            foreach (var error in EsquemaDatos.Validar(esquema))
            {
                context.AddFailure("EsquemaDatosJson", error);
            }
        });
    }
}
