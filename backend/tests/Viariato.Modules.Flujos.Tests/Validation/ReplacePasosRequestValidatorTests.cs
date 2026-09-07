using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Validation;

namespace Viariato.Modules.Flujos.Tests.Validation;

public sealed class ReplacePasosRequestValidatorTests
{
    private readonly ReplacePasosRequestValidator _validator = new();

    [Fact]
    public void Validate_AcceptsContiguousLinearSteps()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Descargar", "Rpa", null, Guid.NewGuid(), null),
            new FlujoPasoDefInput(2, "Validar", "Interno", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsEmptyStepList()
    {
        var request = new ReplacePasosRequest([]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsDuplicateOrden()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Descargar", "Rpa", null, Guid.NewGuid(), null),
            new FlujoPasoDefInput(1, "Validar", "Interno", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsNonContiguousOrden()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Descargar", "Rpa", null, Guid.NewGuid(), null),
            new FlujoPasoDefInput(3, "Validar", "Interno", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsAgenteStepWithoutAgenteDefinicionId()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Analizar", "Agente", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_AcceptsAgenteStepWithAgenteDefinicionId()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Analizar", "Agente", Guid.NewGuid(), null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsRpaStepWithoutServicioId()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Descargar", "Rpa", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_AcceptsRpaStepWithServicioId()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Descargar", "Rpa", null, Guid.NewGuid(), null),
        ]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsDecisionStepWithMissingConfiguracion()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Revisar", "Decision", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsDecisionStepReferencingMissingOrden()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Revisar", "Decision", null, null, """{"ordenSiVerdadero":2,"ordenSiFalso":3}"""),
            new FlujoPasoDefInput(2, "OK", "Interno", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_AcceptsDecisionStepReferencingExistingOrdenes()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Revisar", "Decision", null, null, """{"ordenSiVerdadero":2,"ordenSiFalso":3}"""),
            new FlujoPasoDefInput(2, "OK", "Interno", null, null, null),
            new FlujoPasoDefInput(3, "Error", "Interno", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsUnknownTipoPaso()
    {
        var request = new ReplacePasosRequest([
            new FlujoPasoDefInput(1, "Desconocido", "NoExiste", null, null, null),
        ]);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }
}
