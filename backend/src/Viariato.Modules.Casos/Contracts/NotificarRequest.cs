using System.Text.Json;

namespace Viariato.Modules.Casos.Contracts;

/// <param name="Evento">A code the process's notification rules listen for, e.g. <c>precio_bajado</c>.</param>
/// <param name="Datos">Whatever the notice should be able to show, as an object: rules write it with <c>{evento.campo}</c>.</param>
public sealed record NotificarRequest(string Evento, JsonElement? Datos);
