using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Viariato.Modules.RpaFleet.Options;

namespace Viariato.Modules.RpaFleet.Security;

/// <summary>Encrypts and decrypts the secret part of a credential.</summary>
internal interface ICredencialProtector
{
    string Proteger(string texto, Guid credencialId);

    /// <exception cref="CryptographicException">The value was tampered with, belongs to another credential,
    /// or was encrypted with a different master key.</exception>
    string Revelar(string protegido, Guid credencialId);
}

/// <summary>
/// AES-256-GCM. Stored as base64 of <c>[version][nonce 12][tag 16][ciphertext]</c>, with a fresh random nonce
/// per encryption. The credential's id is bound in as associated data, so a ciphertext copied from one row to
/// another (by someone with write access to the table) fails authentication instead of decrypting.
/// </summary>
internal sealed class AesGcmCredencialProtector(IOptions<CredencialesOptions> opciones) : ICredencialProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _clave = opciones.Value.Clave();

    public string Proteger(string texto, Guid credencialId)
    {
        var claro = Encoding.UTF8.GetBytes(texto);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cifrado = new byte[claro.Length];
            var etiqueta = new byte[TagSize];

            using var aes = new AesGcm(_clave, TagSize);
            aes.Encrypt(nonce, claro, cifrado, etiqueta, credencialId.ToByteArray());

            var resultado = new byte[1 + NonceSize + TagSize + cifrado.Length];
            resultado[0] = Version;
            nonce.CopyTo(resultado, 1);
            etiqueta.CopyTo(resultado, 1 + NonceSize);
            cifrado.CopyTo(resultado, 1 + NonceSize + TagSize);
            return Convert.ToBase64String(resultado);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(claro);
        }
    }

    public string Revelar(string protegido, Guid credencialId)
    {
        byte[] datos;
        try
        {
            datos = Convert.FromBase64String(protegido);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("El valor protegido no es base64 válido.", ex);
        }

        if (datos.Length < 1 + NonceSize + TagSize || datos[0] != Version)
        {
            throw new CryptographicException("El valor protegido tiene un formato o una versión desconocidos.");
        }

        var nonce = datos.AsSpan(1, NonceSize);
        var etiqueta = datos.AsSpan(1 + NonceSize, TagSize);
        var cifrado = datos.AsSpan(1 + NonceSize + TagSize);
        var claro = new byte[cifrado.Length];

        try
        {
            using var aes = new AesGcm(_clave, TagSize);
            aes.Decrypt(nonce, cifrado, etiqueta, claro, credencialId.ToByteArray());
            return Encoding.UTF8.GetString(claro);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(claro);
        }
    }
}
