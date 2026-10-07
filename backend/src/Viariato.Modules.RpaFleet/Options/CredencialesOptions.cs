using System.Security.Cryptography;

namespace Viariato.Modules.RpaFleet.Options;

public sealed class CredencialesOptions
{
    public const string SectionName = "Credenciales";

    public const string MensajeClaveInvalida =
        "Credenciales:ClaveCifrado debe ser base64 de exactamente 32 bytes (p. ej. `openssl rand -base64 32`).";

    /// <summary>Master key that encrypts every stored password: base64 of exactly 32 bytes (AES-256).
    /// Lives in the environment, never in the database — a database dump alone reveals nothing. Losing it
    /// makes every stored password unrecoverable, so it must be backed up separately.</summary>
    public string ClaveCifrado { get; set; } = string.Empty;

    public static bool EsClaveValida(string? valor) => TryDecodificar(valor, out _);

    internal byte[] Clave() =>
        TryDecodificar(ClaveCifrado, out var clave) ? clave : throw new InvalidOperationException(MensajeClaveInvalida);

    private static bool TryDecodificar(string? valor, out byte[] clave)
    {
        clave = [];
        if (string.IsNullOrWhiteSpace(valor)) return false;

        try
        {
            clave = Convert.FromBase64String(valor.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        if (clave.Length == 32) return true;

        CryptographicOperations.ZeroMemory(clave);
        clave = [];
        return false;
    }
}
