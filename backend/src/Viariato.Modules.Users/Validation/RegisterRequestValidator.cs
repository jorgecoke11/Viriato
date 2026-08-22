using FluentValidation;
using Viariato.Modules.Users.Contracts;

namespace Viariato.Modules.Users.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12);
        RuleFor(x => x.DisplayName).NotEmpty();
    }
}
