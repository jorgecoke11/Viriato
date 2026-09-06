using FluentValidation;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Validation;

public sealed class ResolverRevisionRequestValidator : AbstractValidator<ResolverRevisionRequest>
{
    public ResolverRevisionRequestValidator()
    {
        RuleFor(x => x.Decision)
            .Must(d => Enum.TryParse<RevisionDecision>(d, true, out _))
            .WithMessage("Decision debe ser 'Aprobada' o 'Rechazada'.");
        RuleFor(x => x.Comentario).MaximumLength(2000);
    }
}
