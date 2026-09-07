using System.Security.Cryptography;
using System.Text;

namespace Viariato.Modules.RpaFleet.Auth;

/// <summary>Generates and hashes Despliegue API keys. Unlike a user password, an API key is
/// high-entropy and machine-chosen, so a plain salted SHA-256 hash is the right trade-off — no need
/// for a slow KDF like the one Identity uses for human passwords.</summary>
public static class ApiKeyGenerator
{
    private const string Prefix = "rpa_";

    public static (string RawKey, string Hash, string DisplayPrefix) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var rawKey = $"{Prefix}{token}";
        return (rawKey, Hash(rawKey), rawKey[..Math.Min(12, rawKey.Length)]);
    }

    public static string Hash(string rawKey) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
}
