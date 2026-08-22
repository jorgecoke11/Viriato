using FluentValidation;
using Viariato.Modules.Users.Contracts;

namespace Viariato.Modules.Users.Validation;

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
