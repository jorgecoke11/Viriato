using FluentValidation;
using Viariato.Modules.Flujos.Contracts;

namespace Viariato.Modules.Flujos.Validation;

public sealed class UpdateStorageConfigRequestValidator : AbstractValidator<UpdateStorageConfigRequest>
{
    public UpdateStorageConfigRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200).When(x => x.Nombre is not null);
    }
}
