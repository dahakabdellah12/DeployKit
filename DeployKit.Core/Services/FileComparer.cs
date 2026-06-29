using DeployKit.Core.Models;

namespace DeployKit.Core.Services;

public class FileComparer
{
    private readonly HashService _hashService = new();

    public async Task<ComparisonResult> CompareAsync(string oldDir, string newDir)
    {
        var result = new ComparisonResult();

        var oldFiles = GetRelativePaths(oldDir);
        var newFiles = GetRelativePaths(newDir);

        var allPaths = oldFiles.Keys.Union(newFiles.Keys).OrderBy(p => p).ToList();

        foreach (var relativePath in allPaths)
        {
            var inOld = oldFiles.TryGetValue(relativePath, out var oldFullPath);
            var inNew = newFiles.TryGetValue(relativePath, out var newFullPath);

            if (!inNew)
            {
                var oldSize = new FileInfo(oldFullPath!).Length;
                var oldHash = await _hashService.ComputeHashAsync(oldFullPath!);
                result.Deleted.Add(new ChangedFile
                {
                    RelativePath = relativePath,
                    OldHash = oldHash,
                    OldSize = oldSize,
                    ChangeType = FileChangeType.Deleted
                });
            }
            else if (!inOld)
            {
                var newSize = new FileInfo(newFullPath!).Length;
                var newHash = await _hashService.ComputeHashAsync(newFullPath!);
                result.Added.Add(new ChangedFile
                {
                    RelativePath = relativePath,
                    NewHash = newHash,
                    NewSize = newSize,
                    ChangeType = FileChangeType.Added
                });
            }
            else
            {
                var oldHash = await _hashService.ComputeHashAsync(oldFullPath!);
                var newHash = await _hashService.ComputeHashAsync(newFullPath!);
                var oldSize = new FileInfo(oldFullPath!).Length;
                var newSize = new FileInfo(newFullPath!).Length;

                if (oldHash != newHash)
                {
                    result.Modified.Add(new ChangedFile
                    {
                        RelativePath = relativePath,
                        OldHash = oldHash,
                        NewHash = newHash,
                        OldSize = oldSize,
                        NewSize = newSize,
                        ChangeType = FileChangeType.Modified
                    });
                }
                else
                {
                    result.Unchanged.Add(new ChangedFile
                    {
                        RelativePath = relativePath,
                        OldHash = oldHash,
                        NewHash = newHash,
                        OldSize = oldSize,
                        NewSize = newSize,
                        ChangeType = FileChangeType.Unchanged
                    });
                }
            }
        }

        return result;
    }

    private static Dictionary<string, string> GetRelativePaths(string directory)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file);
            files[relative] = file;
        }
        return files;
    }
}
