using FluentValidation;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Validation;

public sealed class UpdateDespliegueRequestValidator : AbstractValidator<UpdateDespliegueRequest>
{
    public UpdateDespliegueRequestValidator()
    {
        RuleFor(x => x.VistaEnDirecto).Must(EsDe<CalidadVistaEnDirecto>).When(x => x.VistaEnDirecto is not null)
            .WithMessage("La vista en directo debe ser Apagada, Baja, Media o Alta.");
        RuleFor(x => x.GrabacionDeVideo).Must(EsDe<GrabacionDeVideo>).When(x => x.GrabacionDeVideo is not null)
            .WithMessage("La grabación de vídeo debe ser SegunRobot, Activa o Apagada.");
    }

    // Names only: Enum.TryParse would also take "7".
    private static bool EsDe<T>(string? valor) where T : struct, Enum =>
        Enum.GetNames<T>().Contains(valor, StringComparer.OrdinalIgnoreCase);
}
