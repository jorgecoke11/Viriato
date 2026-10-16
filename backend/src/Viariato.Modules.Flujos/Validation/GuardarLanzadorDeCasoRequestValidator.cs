using FluentValidation;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Flujos.Validation;

public sealed class GuardarLanzadorDeCasoRequestValidator : AbstractValidator<GuardarLanzadorDeCasoRequest>
{
    public GuardarLanzadorDeCasoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descripcion).MaximumLength(300);
        RuleFor(x => x.CreadorDeCasoId).NotEmpty();

        RuleFor(x => x.Cron).NotEmpty().MaximumLength(CronDeLanzador.LongitudMaxima).Custom((cron, context) =>
        {
            if (string.IsNullOrWhiteSpace(cron)) return;
            if (!CronDeLanzador.TryLeer(cron, out _, out var error)) context.AddFailure("Cron", error!);
        });

        RuleFor(x => x.ZonaHoraria).NotEmpty().MaximumLength(100).Must(z => CronDeLanzador.BuscarZona(z) is not null)
            .WithMessage("No conozco esa zona horaria. Usa un nombre como Europe/Madrid o America/New_York.");

        RuleFor(x => x.ToleranciaMinutos).InclusiveBetween(1, CronDeLanzador.MaximoDeToleranciaMinutos)
            .WithMessage("La tolerancia va de 1 minuto a 24 horas.");

        RuleFor(x => x.DatosJson).Custom((datos, context) =>
        {
            if (!DatosDeLanzador.TryElementos(datos, out _, out var error)) context.AddFailure("DatosJson", error!);
        });
    }
}
