using System.Security.Cryptography;
using System.Text;

namespace FlexFamilyCalendar.Services;

/// <summary>AES-256-CBC encryption for API keys. Key lives in a restricted .keystore file.</summary>
public static class SecretService
{
    private static byte[]? _key;

    public static void Initialize(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        var keyFile = Path.Combine(dataDir, ".keystore");

        if (File.Exists(keyFile))
        {
            _key = Convert.FromBase64String(File.ReadAllText(keyFile).Trim());
        }
        else
        {
            _key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllText(keyFile, Convert.ToBase64String(_key));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        LogService.Debug("SecretService initialisiert");
    }

    public static string Encrypt(string plaintext)
    {
        EnsureInitialized();
        using var aes = Aes.Create();
        aes.Key = _key!;
        aes.GenerateIV();
        var encrypted = aes.EncryptCbc(Encoding.UTF8.GetBytes(plaintext), aes.IV);
        var result = new byte[aes.IV.Length + encrypted.Length];
        aes.IV.CopyTo(result, 0);
        encrypted.CopyTo(result, aes.IV.Length);
        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherBase64)
    {
        EnsureInitialized();
        var data = Convert.FromBase64String(cipherBase64);
        using var aes = Aes.Create();
        aes.Key = _key!;
        var iv = data[..16];
        var cipher = data[16..];
        return Encoding.UTF8.GetString(aes.DecryptCbc(cipher, iv));
    }

    private static void EnsureInitialized()
    {
        if (_key == null) throw new InvalidOperationException("SecretService wurde nicht initialisiert");
    }
}
