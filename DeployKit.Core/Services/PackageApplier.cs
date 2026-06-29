using System.IO.Compression;
using System.Text.Json;
using DeployKit.Core.Models;

namespace DeployKit.Core.Services;

public class PackageApplier
{
    private readonly HashService _hashService = new();
    private readonly BinaryPatchService _patchService = new();
    private readonly RollbackService _rollback;

    public string? BackupPath { get; private set; }

    public PackageApplier(string backupDir)
    {
        _rollback = new RollbackService(backupDir);
    }

    public async Task<PackageManifest> LoadManifestAsync(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("manifest.json not found in package");

        using var reader = new StreamReader(entry.Open());
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<PackageManifest>(json)
            ?? throw new InvalidOperationException("Invalid manifest.json");
    }

    public async Task ApplyAsync(string packagePath, string targetDir)
    {
        var manifest = await LoadManifestAsync(packagePath);

        BackupPath = _rollback.CreateBackup(targetDir);

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);

            foreach (var deleted in manifest.Deleted)
            {
                var fullPath = Path.Combine(targetDir, deleted.Path);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                var dir = Path.GetDirectoryName(fullPath);
                if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }

            foreach (var added in manifest.Added)
            {
                var entry = archive.GetEntry(added.Path)
                    ?? throw new InvalidOperationException($"File not found in package: {added.Path}");
                var fullPath = Path.Combine(targetDir, added.Path);
                var dir = Path.GetDirectoryName(fullPath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using var entryStream = entry.Open();
                using var fileStream = File.Create(fullPath);
                await entryStream.CopyToAsync(fileStream);
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "DeployKit", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                foreach (var modified in manifest.Modified)
                {
                    var fullPath = Path.Combine(targetDir, modified.Path);
                    var dir = Path.GetDirectoryName(fullPath)!;
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (modified.IsPatch && modified.PatchFile != null)
                    {
                        var patchEntry = archive.GetEntry(modified.PatchFile)
                            ?? throw new InvalidOperationException($"Patch not found in package: {modified.PatchFile}");

                        var oldFile = fullPath;
                        var patchFile = Path.Combine(tempDir, Path.GetFileName(modified.PatchFile));

                        using (var patchStream = patchEntry.Open())
                        using (var fs = File.Create(patchFile))
                            await patchStream.CopyToAsync(fs);

                        await _patchService.ApplyPatchAsync(oldFile, patchFile, fullPath);
                    }
                    else
                    {
                        var entry = archive.GetEntry(modified.Path)
                            ?? throw new InvalidOperationException($"File not found in package: {modified.Path}");

                        using var entryStream = entry.Open();
                        using var fileStream = File.Create(fullPath);
                        await entryStream.CopyToAsync(fileStream);
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }
        catch
        {
            _rollback.Restore(BackupPath, targetDir);
            _rollback.Cleanup(BackupPath);
            BackupPath = null;
            throw;
        }
    }
}
