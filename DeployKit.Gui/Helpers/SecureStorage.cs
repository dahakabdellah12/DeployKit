using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DeployKit.Gui.Helpers;

internal static class SecureStorage
{
    private static readonly byte[] Entropy;

    static SecureStorage()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeployKit");
        Directory.CreateDirectory(dir);
        var entropyPath = Path.Combine(dir, ".entropy");
        if (File.Exists(entropyPath) && new FileInfo(entropyPath).Length == 16)
        {
            Entropy = File.ReadAllBytes(entropyPath);
        }
        else
        {
            Entropy = RandomNumberGenerator.GetBytes(16);
            File.WriteAllBytes(entropyPath, Entropy);
        }
    }

    public static string? Encrypt(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch { return null; }
    }

    public static string? Decrypt(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;
        try
        {
            var bytes = Convert.FromBase64String(encrypted);
            var decrypted = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch { return null; }
    }
}
