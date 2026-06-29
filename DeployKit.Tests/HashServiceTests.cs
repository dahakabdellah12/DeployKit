using DeployKit.Core.Services;

namespace DeployKit.Tests;

public class HashServiceTests
{
    private readonly HashService _hash = new();

    [Fact]
    public async Task ComputeHash_ReturnsConsistentHash()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "Hello World");
            var hash1 = await _hash.ComputeHashAsync(path);
            var hash2 = await _hash.ComputeHashAsync(path);
            Assert.Equal(hash1, hash2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ComputeHash_DifferentFiles_DifferentHashes()
    {
        var path1 = Path.GetTempFileName();
        var path2 = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path1, "Hello World");
            await File.WriteAllTextAsync(path2, "Hello World!");
            var hash1 = await _hash.ComputeHashAsync(path1);
            var hash2 = await _hash.ComputeHashAsync(path2);
            Assert.NotEqual(hash1, hash2);
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public async Task ComputeHash_EmptyFile_ReturnsHash()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "");
            var hash = await _hash.ComputeHashAsync(path);
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ComputeHash_LargeFile_ReturnsHash()
    {
        var path = Path.GetTempFileName();
        try
        {
            var data = new byte[10_000_000];
            new Random(42).NextBytes(data);
            await File.WriteAllBytesAsync(path, data);
            var hash = await _hash.ComputeHashAsync(path);
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
