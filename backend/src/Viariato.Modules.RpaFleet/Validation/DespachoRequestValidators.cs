using FluentValidation;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Validation;

internal static class DespachoReglas
{
    public const int MaxSimultaneasPermitido = 50;

    public static bool EsPolitica(string? valor) =>
        Enum.TryParse<PoliticaDespacho>(valor, ignoreCase: true, out var politica) && Enum.IsDefined(politica);

    public static PoliticaDespacho LeerPolitica(string valor) => Enum.Parse<PoliticaDespacho>(valor, ignoreCase: true);
}

public sealed class UpdateDespachoEquipoRequestValidator : AbstractValidator<UpdateDespachoEquipoRequest>
{
    public UpdateDespachoEquipoRequestValidator()
    {
        RuleFor(x => x.MaxEjecucionesSimultaneas!.Value).InclusiveBetween(1, DespachoReglas.MaxSimultaneasPermitido)
            .When(x => x.MaxEjecucionesSimultaneas is not null)
            .WithMessage($"El tope de ejecuciones simultáneas debe estar entre 1 y {DespachoReglas.MaxSimultaneasPermitido}, o vacío para no ponerlo.");
        RuleFor(x => x.Politica).Must(DespachoReglas.EsPolitica).WithMessage("La política debe ser Prioridad o Turnos.");
        RuleFor(x => x.Orden).NotNull()
            .Must(o => o is null || o.All(id => id != Guid.Empty)).WithMessage("El orden contiene un servicio no válido.")
            .Must(o => o is null || o.Distinct().Count() == o.Count).WithMessage("Un servicio no puede aparecer dos veces en el orden.");
    }
}

public sealed class SavePlantillaDespachoRequestValidator : AbstractValidator<SavePlantillaDespachoRequest>
{
    public SavePlantillaDespachoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descripcion).MaximumLength(1000);
        RuleFor(x => x.MaxEjecucionesSimultaneas!.Value).InclusiveBetween(1, DespachoReglas.MaxSimultaneasPermitido)
            .When(x => x.MaxEjecucionesSimultaneas is not null)
            .WithMessage($"El tope de ejecuciones simultáneas debe estar entre 1 y {DespachoReglas.MaxSimultaneasPermitido}, o vacío para no ponerlo.");
        RuleFor(x => x.Politica).Must(DespachoReglas.EsPolitica).WithMessage("La política debe ser Prioridad o Turnos.");
        RuleFor(x => x.Orden).NotNull()
            .Must(o => o is null || o.All(id => id != Guid.Empty)).WithMessage("El orden contiene un servicio no válido.")
            .Must(o => o is null || o.Distinct().Count() == o.Count).WithMessage("Un servicio no puede aparecer dos veces en el orden.");
    }
}
