using System.ComponentModel.DataAnnotations;

namespace Viariato.Shared.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = null!;

    [Required]
    public string Audience { get; set; } = null!;

    [Required]
    public string SigningKey { get; set; } = null!;

    public int AccessTokenMinutes { get; set; } = 15;
}
