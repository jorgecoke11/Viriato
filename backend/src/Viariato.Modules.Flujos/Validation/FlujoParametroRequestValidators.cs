using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class CreateFlujoParametroRequestValidator : AbstractValidator<CreateFlujoParametroRequest>
{
    public CreateFlujoParametroRequestValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9][a-z0-9._-]*$")
            .WithMessage("El código solo puede llevar minúsculas, números, punto, guion y guion bajo, y empezar por letra o número.");
        // Empty is a valid value; only a missing one is not.
        RuleFor(x => x.Valor).NotNull().MaximumLength(2000);
        RuleFor(x => x.Descripcion).MaximumLength(500);
        RuleFor(x => x.Etiqueta).MaximumLength(100);
    }
}

public sealed class UpdateFlujoParametroRequestValidator : AbstractValidator<UpdateFlujoParametroRequest>
{
    public UpdateFlujoParametroRequestValidator()
    {
        RuleFor(x => x.Valor).NotNull().MaximumLength(2000);
        RuleFor(x => x.Descripcion).MaximumLength(500);
        RuleFor(x => x.Etiqueta).MaximumLength(100);
    }
}

public sealed class GuardarParametrosEditablesRequestValidator : AbstractValidator<GuardarParametrosEditablesRequest>
{
    public GuardarParametrosEditablesRequestValidator()
    {
        RuleFor(x => x.Valores).NotEmpty().Must(v => v.Select(x => x.Id).Distinct().Count() == v.Count)
            .WithMessage("Un parámetro no puede venir dos veces.");
        RuleForEach(x => x.Valores).ChildRules(v => v.RuleFor(x => x.Valor).NotNull().MaximumLength(2000));
    }
}
