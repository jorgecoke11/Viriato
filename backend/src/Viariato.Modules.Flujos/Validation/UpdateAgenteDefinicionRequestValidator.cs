using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class UpdateAgenteDefinicionRequestValidator : AbstractValidator<UpdateAgenteDefinicionRequest>
{
    public UpdateAgenteDefinicionRequestValidator()
    {
        RuleFor(x => x.Nombre).MaximumLength(200).When(x => x.Nombre is not null);
        RuleFor(x => x.Descripcion).MaximumLength(1000).When(x => x.Descripcion is not null);
        RuleFor(x => x.Modelo).MaximumLength(100).When(x => x.Modelo is not null);
        RuleFor(x => x.SystemPrompt).NotEmpty().When(x => x.SystemPrompt is not null);
    }
}
