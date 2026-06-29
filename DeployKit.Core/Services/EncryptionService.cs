using System.Security.Cryptography;

namespace DeployKit.Core.Services;

public class EncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes (256-bit)", nameof(key));
        _key = key;
    }

    public EncryptionService(string base64Key)
        : this(Convert.FromBase64String(base64Key))
    {
    }

    public static string GenerateKey()
    {
        var key = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return Convert.ToBase64String(key);
    }

    public async Task EncryptAsync(Stream input, Stream output)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        await output.WriteAsync(aes.IV, 0, aes.IV.Length);

        using var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await input.CopyToAsync(crypto);
        crypto.FlushFinalBlock();
    }

    public async Task DecryptAsync(Stream input, Stream output)
    {
        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[16];
        var read = await input.ReadAsync(iv, 0, 16);
        if (read != 16)
            throw new InvalidDataException("Encrypted data is too short");
        aes.IV = iv;

        using var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
        await crypto.CopyToAsync(output);
    }

    public async Task<byte[]> EncryptAsync(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        await EncryptAsync(input, output);
        return output.ToArray();
    }

    public async Task<byte[]> DecryptAsync(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        await DecryptAsync(input, output);
        return output.ToArray();
    }

    public async Task EncryptFileAsync(string inputPath, string outputPath)
    {
        using var input = File.OpenRead(inputPath);
        using var output = File.Create(outputPath);
        await EncryptAsync(input, output);
    }

    public async Task DecryptFileAsync(string inputPath, string outputPath)
    {
        using var input = File.OpenRead(inputPath);
        using var output = File.Create(outputPath);
        await DecryptAsync(input, output);
    }
}
