namespace Viariato.Modules.Flujos.Domain;

/// <summary>Reusable configuration for an agentic step: which model, system prompt and tools it may
/// use. A single definition can be referenced by many <see cref="FlujoPasoDef"/> rows.</summary>
public sealed class AgenteDefinicion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public required string Modelo { get; set; }

    public required string SystemPrompt { get; set; }

    /// <summary>jsonb array of tool names this agent may call.</summary>
    public string? HerramientasPermitidas { get; set; }

    /// <summary>jsonb, model parameters (temperature, etc.).</summary>
    public string? ParametrosJson { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
