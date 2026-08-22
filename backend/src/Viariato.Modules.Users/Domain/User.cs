namespace Viariato.Modules.Users.Domain;

public sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Email { get; set; } = null!;
    public string EmailNormalized { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string BaseCurrency { get; set; } = "EUR";
    public string TimeZone { get; set; } = "Europe/Madrid";
    public string Locale { get; set; } = "es-ES";
    public bool EmailConfirmed { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
