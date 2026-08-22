using FluentValidation;
using Viariato.Modules.Users.Contracts;

namespace Viariato.Modules.Users.Validation;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().When(x => x.DisplayName is not null);
        RuleFor(x => x.BaseCurrency).Length(3).When(x => x.BaseCurrency is not null);
        RuleFor(x => x.TimeZone).NotEmpty().When(x => x.TimeZone is not null);
        RuleFor(x => x.Locale).NotEmpty().When(x => x.Locale is not null);
    }
}
