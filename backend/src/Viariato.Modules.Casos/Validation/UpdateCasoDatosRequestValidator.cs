using System.Text.Json;
using FluentValidation;
using Viariato.Modules.Casos.Contracts;

namespace Viariato.Modules.Casos.Validation;

public sealed class UpdateCasoDatosRequestValidator : AbstractValidator<UpdateCasoDatosRequest>
{
    public UpdateCasoDatosRequestValidator()
    {
        RuleFor(x => x.DatosJson)
            .NotEmpty()
            .Must(EsJsonValido).WithMessage("DatosJson debe ser un JSON válido.");
    }

    private static bool EsJsonValido(string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
