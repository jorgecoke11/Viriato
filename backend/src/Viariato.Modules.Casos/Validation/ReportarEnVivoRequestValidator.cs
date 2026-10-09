using FluentValidation;
using Viariato.ApiContracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class ReportarEnVivoRequestValidator : AbstractValidator<ReportarEnVivoRequest>
{
    public const int MaximoDelMensaje = 200;
    public const int MaximoDeLaUrl = 500;

    public ReportarEnVivoRequestValidator()
    {
        RuleFor(x => x).Must(x => x.Porcentaje is not null || x.Mensaje is not null || x.VistaUrl is not null)
            .WithMessage("Indica al menos el porcentaje, el mensaje o la dirección de la vista en directo.");
        RuleFor(x => x.Porcentaje).InclusiveBetween(0, 100).When(x => x.Porcentaje is not null)
            .WithMessage("El porcentaje va de 0 a 100.");
        RuleFor(x => x.Mensaje).MaximumLength(MaximoDelMensaje);
        RuleFor(x => x.VistaUrl).MaximumLength(MaximoDeLaUrl);
        // It ends up as a link in the interface: only web addresses ("javascript:" and the like never get that far). Empty clears it.
        RuleFor(x => x.VistaUrl).Must(EsDireccionWeb).When(x => !string.IsNullOrWhiteSpace(x.VistaUrl))
            .WithMessage("La dirección de la vista en directo tiene que ser una dirección http o https completa.");
    }

    public static bool EsDireccionWeb(string? texto) =>
        Uri.TryCreate(texto?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host);
}
