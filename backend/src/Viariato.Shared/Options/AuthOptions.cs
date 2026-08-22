namespace Viariato.Shared.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int RefreshTokenDays { get; set; } = 30;
}
