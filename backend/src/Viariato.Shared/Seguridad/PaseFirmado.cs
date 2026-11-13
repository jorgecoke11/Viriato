using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Viariato.Shared.Seguridad;

/// <summary>
/// A pass that names someone (an id) until a given time, and that only whoever holds the key can make:
/// <c>{prefijo}{id}.{caduca}.{firma}</c>. Nothing is stored: it is checked by signing it again.
///
/// The signature uses a key derived from the secret and a <c>proposito</c>, so a pass made for one thing (watching a robot's
/// screen) is never valid for another (acting as a robot), even with the same secret — and never an access token.
/// </summary>
public static class PaseFirmado
{
    /// <param name="prefijo">Tells this kind of pass apart at a glance (e.g. <c>rpl_</c>); part of what is signed.</param>
    public static string Emitir(ReadOnlySpan<byte> secreto, string proposito, Guid id, DateTimeOffset caduca, string prefijo = "")
    {
        var contenido = $"{prefijo}{id:N}.{caduca.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}";
        return $"{contenido}.{Convert.ToHexString(Firmar(secreto, proposito, contenido))}";
    }

    /// <summary>The id the pass names, or null if it is not one: forged, expired, of another secret or purpose, or malformed.</summary>
    public static Guid? Validar(ReadOnlySpan<byte> secreto, string proposito, string? pase, DateTimeOffset ahora, string prefijo = "")
    {
        if (string.IsNullOrEmpty(pase) || !pase.StartsWith(prefijo, StringComparison.Ordinal)) return null;

        var partes = pase[prefijo.Length..].Split('.');
        if (partes.Length != 3
            || !Guid.TryParseExact(partes[0], "N", out var id)
            || !long.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out var caduca))
        {
            return null;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(caduca) <= ahora) return null;

        byte[] firma;
        try
        {
            firma = Convert.FromHexString(partes[2]);
        }
        catch (FormatException)
        {
            return null;
        }

        return CryptographicOperations.FixedTimeEquals(firma, Firmar(secreto, proposito, $"{prefijo}{partes[0]}.{partes[1]}")) ? id : null;
    }

    private static byte[] Firmar(ReadOnlySpan<byte> secreto, string proposito, string contenido)
    {
        var clave = HMACSHA256.HashData(secreto, Encoding.UTF8.GetBytes(proposito));
        return HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes(contenido));
    }
}
