namespace Viariato.ApiContracts;

/// <summary>A robot asking Viriato to open a new Caso. Only a Despliegue an admin has authorised to create
/// Casos in one particular process can do this, and the Caso always lands in that process — there is no field
/// to pick another one.</summary>
/// <param name="Titulo">The Caso's title, as shown in the lists.</param>
/// <param name="DatosJson">The Caso's business data (a JSON object). It is what the robots of the process will read.</param>
/// <param name="TipoCaso">Name of one of the process's case types (e.g. a brand), or null for none.</param>
/// <param name="EstadoNegocioCodigo">Code of the business state the Caso starts in, or null for none.</param>
/// <param name="PasoInicial">Name of the step of the process the Caso starts at (the steps before it are recorded as
/// skipped), or null to start at the first one. A robot cannot create a Caso that would begin at a step of its own
/// Servicio — it would just create more of itself — so a launcher that is itself step 1 of the process names the
/// step after it.</param>
/// <param name="Prioridad">The priority of the execution this opens (the one for the step the Caso starts at): the higher the
/// number, the sooner a robot takes it, among the executions waiting for the same service. 0 is the ordinary one and the
/// default; negative values wait behind it. Between -1000 and 1000. The executions the Caso creates later (its next
/// steps) start at 0 like any other, and a person can change an execution's priority while it waits.</param>
public sealed record CrearCasoRobotRequest(
    string Titulo,
    string? DatosJson,
    string? TipoCaso = null,
    string? EstadoNegocioCodigo = null,
    string? PasoInicial = null,
    int Prioridad = 0);
