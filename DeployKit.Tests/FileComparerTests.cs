using DeployKit.Core.Services;
using DeployKit.Core.Models;

namespace DeployKit.Tests;

public class FileComparerTests
{
    private readonly FileComparer _comparer = new();

    [Fact]
    public async Task CompareAsync_IdenticalDirectories_NoChanges()
    {
        using var td = new TempDir();
        for (int i = 0; i < 3; i++)
            await File.WriteAllTextAsync(Path.Combine(td.Path, $"file{i}.txt"), "same");

        var result = await _comparer.CompareAsync(td.Path, td.Path);
        Assert.Empty(result.Added);
        Assert.Empty(result.Deleted);
        Assert.Empty(result.Modified);
    }

    [Fact]
    public async Task CompareAsync_NewFile_Detected()
    {
        using var oldDir = new TempDir();
        using var newDir = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(newDir.Path, "new.txt"), "new file");

        var result = await _comparer.CompareAsync(oldDir.Path, newDir.Path);
        Assert.Single(result.Added);
        Assert.Contains(result.Added, f => f.RelativePath == "new.txt");
    }

    [Fact]
    public async Task CompareAsync_DeletedFile_Detected()
    {
        using var oldDir = new TempDir();
        using var newDir = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(oldDir.Path, "old.txt"), "old file");

        var result = await _comparer.CompareAsync(oldDir.Path, newDir.Path);
        Assert.Single(result.Deleted);
        Assert.Contains(result.Deleted, f => f.RelativePath == "old.txt");
    }

    [Fact]
    public async Task CompareAsync_ModifiedFile_Detected()
    {
        using var oldDir = new TempDir();
        using var newDir = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(oldDir.Path, "file.txt"), "old content");
        await File.WriteAllTextAsync(Path.Combine(newDir.Path, "file.txt"), "new content");

        var result = await _comparer.CompareAsync(oldDir.Path, newDir.Path);
        Assert.Single(result.Modified);
    }

    [Fact]
    public async Task CompareAsync_Subdirectories_Handled()
    {
        using var oldDir = new TempDir();
        using var newDir = new TempDir();
        Directory.CreateDirectory(Path.Combine(newDir.Path, "sub"));
        await File.WriteAllTextAsync(Path.Combine(newDir.Path, "sub", "nested.txt"), "nested");

        var result = await _comparer.CompareAsync(oldDir.Path, newDir.Path);
        Assert.Single(result.Added);
        Assert.Equal("sub/nested.txt", result.Added[0].RelativePath.Replace('\\', '/'));
    }
}

public class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DeployKitTest_{Guid.NewGuid():N}");
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { }
    }
}
