using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class CreateAgenteDefinicionRequestValidator : AbstractValidator<CreateAgenteDefinicionRequest>
{
    public CreateAgenteDefinicionRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descripcion).MaximumLength(1000);
        RuleFor(x => x.Modelo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SystemPrompt).NotEmpty();
    }
}
