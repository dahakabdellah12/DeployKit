using DeployKit.Core.Services;
using DeployKit.Core.Models;

namespace DeployKit.Tests;

public class PackageBuildApplyTests
{
    private readonly FileComparer _comparer = new();
    private static readonly byte[] TestKey = "12345678901234567890123456789012"u8.ToArray();

    [Fact]
    public async Task BuildAndApply_FullWorkflow()
    {
        using var oldDir = new TempDir();
        using var newDir = new TempDir();

        await File.WriteAllTextAsync(Path.Combine(oldDir.Path, "common.txt"), "same content");
        await File.WriteAllTextAsync(Path.Combine(oldDir.Path, "to-delete.txt"), "will be deleted");
        var oldBytes = new byte[5000];
        new Random(42).NextBytes(oldBytes);
        await File.WriteAllBytesAsync(Path.Combine(oldDir.Path, "binary.dat"), oldBytes);

        await File.WriteAllTextAsync(Path.Combine(newDir.Path, "common.txt"), "same content");
        await File.WriteAllTextAsync(Path.Combine(newDir.Path, "new-file.txt"), "brand new");
        var newBytes = new byte[5000];
        new Random(43).NextBytes(newBytes);
        await File.WriteAllBytesAsync(Path.Combine(newDir.Path, "binary.dat"), newBytes);

        var comparison = await _comparer.CompareAsync(oldDir.Path, newDir.Path);
        Assert.Single(comparison.Added);
        Assert.Single(comparison.Modified);
        Assert.Single(comparison.Deleted);

        // Build unencrypted package
        var builder = new PackageBuilder();
        var packagePath = Path.Combine(oldDir.Path, "update.dkup");
        await builder.BuildAsync(oldDir.Path, newDir.Path, comparison,
            "TestApp", "1.0.0", "2.0.0", packagePath);
        Assert.True(File.Exists(packagePath));

        // Build encrypted package
        var encBuilder = new PackageBuilder(new EncryptionService(TestKey));
        var encPackagePath = Path.Combine(oldDir.Path, "update_enc.dkup");
        await encBuilder.BuildAsync(oldDir.Path, newDir.Path, comparison,
            "TestApp", "1.0.0", "2.0.0", encPackagePath);
        Assert.True(File.Exists(encPackagePath));

        // Apply encrypted package to fresh directory
        using var applyDir = new TempDir();
        foreach (var f in Directory.GetFiles(oldDir.Path).Where(p => !p.EndsWith(".dkup")))
            File.Copy(f, Path.Combine(applyDir.Path, Path.GetFileName(f)));

        var applier = new PackageApplier(applyDir.Path,
            new EncryptionService(TestKey));
        await applier.ApplyAsync(encPackagePath, applyDir.Path);

        Assert.Equal("same content", await File.ReadAllTextAsync(Path.Combine(applyDir.Path, "common.txt")));
        Assert.Equal("brand new", await File.ReadAllTextAsync(Path.Combine(applyDir.Path, "new-file.txt")));
        Assert.False(File.Exists(Path.Combine(applyDir.Path, "to-delete.txt")));
    }
}
