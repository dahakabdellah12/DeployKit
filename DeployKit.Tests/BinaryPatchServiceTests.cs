using DeployKit.Core.Services;

namespace DeployKit.Tests;

public class BinaryPatchServiceTests
{
    private readonly BinaryPatchService _patcher = new();
    private readonly HashService _hash = new();

    [Fact]
    public async Task CreateAndApplyPatch_SmallFile()
    {
        var oldBytes = "Hello World! This is the original file content."u8.ToArray();
        var newBytes = "Hello World! This is the patched file content."u8.ToArray();

        var oldPath = Path.GetTempFileName();
        var newPath = Path.GetTempFileName();
        var patchPath = Path.GetTempFileName();
        var outputPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(oldPath, oldBytes);
            await File.WriteAllBytesAsync(newPath, newBytes);

            await _patcher.CreatePatchAsync(oldPath, newPath, patchPath);
            await _patcher.ApplyPatchAsync(oldPath, patchPath, outputPath);

            var outputHash = await _hash.ComputeHashAsync(outputPath);
            var expectedHash = await _hash.ComputeHashAsync(newPath);
            Assert.Equal(expectedHash, outputHash);
        }
        finally
        {
            foreach (var p in new[] { oldPath, newPath, patchPath, outputPath })
                try { File.Delete(p); } catch { }
        }
    }

    [Fact]
    public async Task CreateAndApplyPatch_BinaryData()
    {
        var rng = new Random(42);
        var oldBytes = new byte[5000];
        var newBytes = new byte[5000];
        rng.NextBytes(oldBytes);
        rng.NextBytes(newBytes);

        var oldPath = Path.GetTempFileName();
        var newPath = Path.GetTempFileName();
        var patchPath = Path.GetTempFileName();
        var outputPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(oldPath, oldBytes);
            await File.WriteAllBytesAsync(newPath, newBytes);

            await _patcher.CreatePatchAsync(oldPath, newPath, patchPath);
            await _patcher.ApplyPatchAsync(oldPath, patchPath, outputPath);

            var outputHash = await _hash.ComputeHashAsync(outputPath);
            var expectedHash = await _hash.ComputeHashAsync(newPath);
            Assert.Equal(expectedHash, outputHash);
        }
        finally
        {
            foreach (var p in new[] { oldPath, newPath, patchPath, outputPath })
                try { File.Delete(p); } catch { }
        }
    }

    [Fact]
    public async Task CreateAndApplyPatch_IdenticalFiles_EmptyPatch()
    {
        var data = "Identical content across both files."u8.ToArray();
        var oldPath = Path.GetTempFileName();
        var newPath = Path.GetTempFileName();
        var patchPath = Path.GetTempFileName();
        var outputPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(oldPath, data);
            await File.WriteAllBytesAsync(newPath, data);

            await _patcher.CreatePatchAsync(oldPath, newPath, patchPath);
            var patchInfo = new FileInfo(patchPath);
            Assert.True(patchInfo.Length < 100);

            await _patcher.ApplyPatchAsync(oldPath, patchPath, outputPath);
            var outputHash = await _hash.ComputeHashAsync(outputPath);
            var expectedHash = await _hash.ComputeHashAsync(newPath);
            Assert.Equal(expectedHash, outputHash);
        }
        finally
        {
            foreach (var p in new[] { oldPath, newPath, patchPath, outputPath })
                try { File.Delete(p); } catch { }
        }
    }
}
