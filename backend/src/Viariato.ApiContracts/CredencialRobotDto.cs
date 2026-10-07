namespace Viariato.ApiContracts;

/// <summary>What a robot gets back when it asks Viriato for one of the credentials it is allowed to use.
/// The password is only ever sent over this call — the admin screens can set it but never read it back.</summary>
public sealed record CredencialRobotDto(string Nombre, string? Usuario, string Password);
