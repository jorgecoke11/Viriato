using FluentValidation;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Flujos.Validation;

public sealed class GuardarCreadorDeCasoRequestValidator : AbstractValidator<GuardarCreadorDeCasoRequest>
{
    public GuardarCreadorDeCasoRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descripcion).MaximumLength(300);
        RuleFor(x => x.PasoInicialNombre).MaximumLength(200);
        RuleFor(x => x.PlantillaTitulo).MaximumLength(PlantillaDeTitulo.LongitudMaxima);
        RuleFor(x => x.PlantillaTitulo).Custom((plantilla, context) =>
        {
            if (string.IsNullOrWhiteSpace(plantilla)) return;
            var desconocidos = PlantillaDeTitulo.Desconocidos(plantilla);
            if (desconocidos.Count > 0)
            {
                context.AddFailure(
                    "PlantillaTitulo",
                    $"No conozco {string.Join(", ", desconocidos.Select(d => "{" + d + "}"))}. Puedes usar " +
                    $"{string.Join(", ", PlantillaDeTitulo.Variables.Select(v => "{" + v + "}"))} y {{datos.campo}}.");
            }
        });
    }
}
