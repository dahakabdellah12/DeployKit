using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeployKit.Core.Models;

namespace DeployKit.Core.Services;

public class PackageApplier
{
    private readonly HashService _hashService = new();
    private readonly BinaryPatchService _patchService = new();
    private readonly RollbackService _rollback;
    private readonly EncryptionService? _encryption;

    public string? BackupPath { get; private set; }
    private string? _decryptedPath;

    public PackageApplier(string backupDir, EncryptionService? encryption = null)
    {
        _rollback = new RollbackService(backupDir);
        _encryption = encryption;
    }

    private string ResolvePath(string packagePath)
    {
        if (_encryption == null) return packagePath;

        if (_decryptedPath == null)
        {
            _decryptedPath = packagePath + ".dec";
            _encryption.DecryptFileAsync(packagePath, _decryptedPath).GetAwaiter().GetResult();
        }
        return _decryptedPath;
    }

    private void Cleanup()
    {
        if (_decryptedPath != null && File.Exists(_decryptedPath))
        {
            File.Delete(_decryptedPath);
            _decryptedPath = null;
        }
    }

    public async Task<PackageManifest> LoadManifestAsync(string packagePath)
    {
        var actualPath = ResolvePath(packagePath);

        if (!File.Exists(actualPath))
            throw new InvalidOperationException("Package file not found");

        using var archive = ZipFile.OpenRead(actualPath);
        var entry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("manifest.json not found in package");

        using var reader = new StreamReader(entry.Open());
        var json = await reader.ReadToEndAsync();
        var manifest = JsonSerializer.Deserialize<PackageManifest>(json)
            ?? throw new InvalidOperationException("Invalid manifest.json");

        return manifest;
    }

    public async Task ApplyAsync(string packagePath, string targetDir)
    {
        var actualPath = ResolvePath(packagePath);
        var manifest = await LoadManifestAsync(packagePath);

        BackupPath = _rollback.CreateBackup(targetDir);

        try
        {
            using var archive = ZipFile.OpenRead(actualPath);

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

                await RetryableWriteAsync(fullPath, entry.Open);
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

                        await RetryableWriteAsync(patchFile, patchEntry.Open);

                        await _patchService.ApplyPatchAsync(oldFile, patchFile, fullPath);
                    }
                    else
                    {
                        var entry = archive.GetEntry(modified.Path)
                            ?? throw new InvalidOperationException($"File not found in package: {modified.Path}");

                        await RetryableWriteAsync(fullPath, entry.Open);
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
            Cleanup();
            throw;
        }

        Cleanup();
    }

    public async Task ApplyFullPackageAsync(string packagePath, string targetDir)
    {
        var actualPath = ResolvePath(packagePath);
        var manifest = await LoadManifestAsync(packagePath);

        if (!manifest.IsFullPackage)
            throw new InvalidOperationException("Package is not a full package. Use ApplyAsync for delta packages.");

        BackupPath = _rollback.CreateBackup(targetDir);

        try
        {
            using var archive = ZipFile.OpenRead(actualPath);

            var manifestPaths = new HashSet<string>(manifest.AllFiles.Select(f => f.Path));

            foreach (var file in manifest.AllFiles)
            {
                var fullPath = Path.Combine(targetDir, file.Path);
                var dir = Path.GetDirectoryName(fullPath)!;

                if (File.Exists(fullPath))
                {
                    var existingHash = await _hashService.ComputeHashAsync(fullPath);
                    if (string.Equals(existingHash, file.Hash, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var entry = archive.GetEntry(file.Path);
                if (entry == null) continue;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await RetryableWriteAsync(fullPath, entry.Open);
            }

            foreach (var existingFile in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(targetDir, existingFile);
                if (!manifestPaths.Contains(relPath))
                    File.Delete(existingFile);
            }

            foreach (var dir in Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
        }
        catch
        {
            if (BackupPath != null)
            {
                _rollback.Restore(BackupPath, targetDir);
                _rollback.Cleanup(BackupPath);
            }
            BackupPath = null;
            Cleanup();
            throw;
        }

        Cleanup();
    }

    private static async Task RetryableWriteAsync(string path, Func<Stream> openEntry, int maxRetries = 10)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var entryStream = openEntry();
                using var fileStream = File.Create(path);
                await entryStream.CopyToAsync(fileStream);
                return;
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(300 * (attempt + 1));
            }
        }
    }
}
