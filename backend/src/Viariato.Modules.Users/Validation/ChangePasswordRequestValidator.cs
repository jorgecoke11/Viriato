using FluentValidation;
using Viariato.Modules.Users.Contracts;

namespace Viariato.Modules.Users.Validation;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12);
    }
}
