using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Contracts;

/// <summary>Deliberately has no password — not even a masked one. The secret only travels to a robot.</summary>
public sealed record CredencialDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    string? Usuario,
    Guid? ServicioId,
    string? ServicioNombre,
    bool Activo,
    DateTimeOffset? UltimoAccesoAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateCredencialRequest(string Nombre, string? Descripcion, string? Usuario, string Password, Guid? ServicioId);

/// <summary>A full replacement of everything editable except the name: a null <c>ServicioId</c> means
/// "any robot", and a null or empty <c>Password</c> means "keep the stored one".</summary>
public sealed record UpdateCredencialRequest(string? Descripcion, string? Usuario, Guid? ServicioId, bool Activo, string? Password);

public static class CredencialDtoMapper
{
    public static CredencialDto ToDto(this Credencial credencial) => new(
        credencial.Id,
        credencial.Nombre,
        credencial.Descripcion,
        credencial.Usuario,
        credencial.ServicioId,
        credencial.Servicio?.Nombre,
        credencial.Activo,
        credencial.UltimoAccesoAt,
        credencial.CreatedAt,
        credencial.UpdatedAt);
}
