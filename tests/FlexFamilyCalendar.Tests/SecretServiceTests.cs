using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class SecretServiceTests : IDisposable
{
    private readonly string _dir;

    public SecretServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ffc_test_" + Guid.NewGuid().ToString("N"));
        SecretService.Initialize(_dir);
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip()
    {
        const string secret = "sk-super-geheim-äöü-123";
        var cipher = SecretService.Encrypt(secret);
        Assert.Equal(secret, SecretService.Decrypt(cipher));
    }

    [Fact]
    public void Ciphertext_DiffersFromPlaintext()
    {
        const string secret = "mein-api-key";
        Assert.NotEqual(secret, SecretService.Encrypt(secret));
    }

    [Fact]
    public void SameInput_ProducesDifferentCiphertext_DueToRandomIv()
    {
        const string secret = "wiederholung";
        Assert.NotEqual(SecretService.Encrypt(secret), SecretService.Encrypt(secret));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { /* ignore */ }
    }
}
