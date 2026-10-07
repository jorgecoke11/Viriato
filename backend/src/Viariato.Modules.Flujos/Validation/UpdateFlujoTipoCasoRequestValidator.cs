using FluentValidation;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Flujos.Validation;

public sealed class UpdateFlujoTipoCasoRequestValidator : AbstractValidator<UpdateFlujoTipoCasoRequest>
{
    public UpdateFlujoTipoCasoRequestValidator()
    {
        RuleFor(x => x.Nombre).MaximumLength(100).When(x => x.Nombre is not null);
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
