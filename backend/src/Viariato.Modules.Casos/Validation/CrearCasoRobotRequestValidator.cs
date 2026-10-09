using System.Text.Json;
using FluentValidation;
using Viariato.ApiContracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class CrearCasoRobotRequestValidator : AbstractValidator<CrearCasoRobotRequest>
{
    public CrearCasoRobotRequestValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(300);
        RuleFor(x => x.TipoCaso).MaximumLength(200);
        RuleFor(x => x.EstadoNegocioCodigo).MaximumLength(100);
        RuleFor(x => x.PasoInicial).MaximumLength(200);
        RuleFor(x => x.Prioridad).InclusiveBetween(PrioridadDeEjecucion.Minima, PrioridadDeEjecucion.Maxima)
            .WithMessage($"La prioridad debe estar entre {PrioridadDeEjecucion.Minima} y {PrioridadDeEjecucion.Maxima}.");
        RuleFor(x => x.DatosJson)
            .Must(EsObjetoJson).When(x => !string.IsNullOrWhiteSpace(x.DatosJson))
            .WithMessage("DatosJson debe ser un objeto JSON válido.");
    }

    private static bool EsObjetoJson(string? json)
    {
        try
        {
            using var documento = JsonDocument.Parse(json!);
            return documento.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
