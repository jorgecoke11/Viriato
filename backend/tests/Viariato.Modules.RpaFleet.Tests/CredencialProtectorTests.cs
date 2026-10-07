using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Viariato.Modules.RpaFleet.Options;
using Viariato.Modules.RpaFleet.Security;

namespace Viariato.Modules.RpaFleet.Tests;

public sealed class CredencialProtectorTests
{
    private static string ClaveBase64(byte relleno) => Convert.ToBase64String(Enumerable.Repeat(relleno, 32).ToArray());

    private static AesGcmCredencialProtector Protector(byte relleno = 1) =>
        new(Microsoft.Extensions.Options.Options.Create(new CredencialesOptions { ClaveCifrado = ClaveBase64(relleno) }));

    [Theory]
    [InlineData("S3cr3t-pwd!")]
    [InlineData("contraseña con ñ, tildes y emoji 🔐")]
    [InlineData("  espacios al principio y al final  ")]
    [InlineData("a")]
    public void ASecret_RoundTrips_ExactlyAsWritten(string secreto)
    {
        var id = Guid.NewGuid();
        var protector = Protector();

        Assert.Equal(secreto, protector.Revelar(protector.Proteger(secreto, id), id));
    }

    [Fact]
    public void ALongSecret_RoundTrips()
    {
        var secreto = new string('x', 1000);
        var id = Guid.NewGuid();
        var protector = Protector();

        Assert.Equal(secreto, protector.Revelar(protector.Proteger(secreto, id), id));
    }

    [Fact]
    public void TheStoredValue_DoesNotContainTheSecret()
    {
        const string secreto = "S3cr3t-pwd!";
        var protegido = Protector().Proteger(secreto, Guid.NewGuid());

        var bytes = Convert.FromBase64String(protegido);
        Assert.DoesNotContain(secreto, protegido);
        Assert.False(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secreto)) >= 0, "the plaintext bytes must not appear in the ciphertext");
    }

    [Fact]
    public void EncryptingTheSameSecretTwice_GivesDifferentValues()
    {
        var id = Guid.NewGuid();
        var protector = Protector();

        Assert.NotEqual(protector.Proteger("igual", id), protector.Proteger("igual", id));
    }

    [Fact]
    public void ATamperedValue_IsRejected_NotDecryptedToGarbage()
    {
        var id = Guid.NewGuid();
        var protector = Protector();
        var bytes = Convert.FromBase64String(protector.Proteger("secreto", id));
        bytes[^1] ^= 0x01; // flip one bit of the ciphertext

        Assert.Throws<AuthenticationTagMismatchException>(() => protector.Revelar(Convert.ToBase64String(bytes), id));
    }

    [Fact]
    public void ACiphertextCopiedToAnotherCredential_DoesNotDecrypt()
    {
        var protector = Protector();
        var protegido = protector.Proteger("secreto", Guid.NewGuid());

        Assert.ThrowsAny<CryptographicException>(() => protector.Revelar(protegido, Guid.NewGuid()));
    }

    [Fact]
    public void AValueEncryptedWithAnotherMasterKey_DoesNotDecrypt()
    {
        var id = Guid.NewGuid();
        var protegido = Protector(1).Proteger("secreto", id);

        Assert.ThrowsAny<CryptographicException>(() => Protector(2).Revelar(protegido, id));
    }

    [Theory]
    [InlineData("esto no es base64!!")]
    [InlineData("AAAA")] // valid base64, far too short
    public void GarbageInput_IsReportedAsACryptographicFailure(string basura)
    {
        Assert.ThrowsAny<CryptographicException>(() => Protector().Revelar(basura, Guid.NewGuid()));
    }

    [Fact]
    public void AnUnknownVersion_IsRejected()
    {
        var id = Guid.NewGuid();
        var protector = Protector();
        var bytes = Convert.FromBase64String(protector.Proteger("secreto", id));
        bytes[0] = 99;

        Assert.Throws<CryptographicException>(() => protector.Revelar(Convert.ToBase64String(bytes), id));
    }

    [Fact]
    public void AValidMasterKey_IsExactly32Base64Bytes()
    {
        Assert.True(CredencialesOptions.EsClaveValida(ClaveBase64(7)));
        Assert.True(CredencialesOptions.EsClaveValida("  " + ClaveBase64(7) + "  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CHANGE_ME_BASE64_DE_32_BYTES")] // the placeholder in .env.prod.example must never pass
    [InlineData("no-es-base64")]
    public void AMissingOrMalformedMasterKey_IsInvalid(string? clave)
    {
        Assert.False(CredencialesOptions.EsClaveValida(clave));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void AKeyOfTheWrongLength_IsInvalid(int bytes)
    {
        Assert.False(CredencialesOptions.EsClaveValida(Convert.ToBase64String(new byte[bytes])));
    }

    [Fact]
    public void BuildingTheProtectorWithAnInvalidKey_FailsLoudly()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new AesGcmCredencialProtector(Microsoft.Extensions.Options.Options.Create(new CredencialesOptions { ClaveCifrado = "mala" })));

        Assert.Contains("32 bytes", ex.Message);
    }
}
