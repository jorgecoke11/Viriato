using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class UpdateFlujoEstadoDefRequestValidator : AbstractValidator<UpdateFlujoEstadoDefRequest>
{
    public UpdateFlujoEstadoDefRequestValidator()
    {
        RuleFor(x => x.Display).MaximumLength(100).When(x => x.Display is not null);
    }
}
