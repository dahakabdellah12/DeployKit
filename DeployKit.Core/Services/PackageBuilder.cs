using System.IO.Compression;
using System.Text.Json;
using DeployKit.Core.Models;

namespace DeployKit.Core.Services;

public class PackageBuilder
{
    private readonly HashService _hashService = new();
    private readonly BinaryPatchService _patchService = new();
    private readonly EncryptionService? _encryption;

    public PackageBuilder(EncryptionService? encryption = null)
    {
        _encryption = encryption;
    }

    public async Task<string> BuildAsync(
        string oldDir, string newDir, ComparisonResult comparison,
        string appName, string sourceVersion, string targetVersion,
        string outputPath)
    {
        var manifest = new PackageManifest
        {
            AppName = appName,
            SourceVersion = sourceVersion,
            TargetVersion = targetVersion,
            Added = comparison.Added.Select(f => new FileEntry
            {
                Path = f.RelativePath,
                Hash = f.NewHash,
                Size = f.NewSize
            }).ToList(),
            Modified = comparison.Modified.Select(f => new ModifiedEntry
            {
                Path = f.RelativePath,
                OldHash = f.OldHash,
                NewHash = f.NewHash,
                Size = f.NewSize,
                IsPatch = f.IsPatch,
                PatchFile = f.IsPatch ? $"patches/{f.NewHash}.patch" : null
            }).ToList(),
            Deleted = comparison.Deleted.Select(f => new DeletedEntry
            {
                Path = f.RelativePath
            }).ToList()
        };

        var dir = Path.GetDirectoryName(outputPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        var tempDir = Path.Combine(Path.GetTempPath(), "DeployKit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var file in comparison.Modified)
            {
                var oldFullPath = Path.Combine(oldDir, file.RelativePath);
                var newFullPath = Path.Combine(newDir, file.RelativePath);

                var patchPath = Path.Combine(tempDir, $"{file.NewHash}.patch");
                await _patchService.CreatePatchAsync(oldFullPath, newFullPath, patchPath);

                var patchInfo = new FileInfo(patchPath);
                file.PatchSize = patchInfo.Length;
                file.IsPatch = patchInfo.Length < file.NewSize * 0.9;
            }

            manifest.Modified = comparison.Modified.Select(f => new ModifiedEntry
            {
                Path = f.RelativePath,
                OldHash = f.OldHash,
                NewHash = f.NewHash,
                Size = f.IsPatch ? f.PatchSize : f.NewSize,
                IsPatch = f.IsPatch,
                PatchFile = f.IsPatch ? $"patches/{f.NewHash}.patch" : null
            }).ToList();

            using (var stream = File.Create(outputPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var manifestEntry = archive.CreateEntry("manifest.json");
                using (var writer = new StreamWriter(manifestEntry.Open()))
                {
                    var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await writer.WriteAsync(json);
                }

                foreach (var file in comparison.Added)
                {
                    var fullPath = Path.Combine(newDir, file.RelativePath);
                    var entry = archive.CreateEntry(file.RelativePath, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(fullPath);
                    await fileStream.CopyToAsync(entryStream);
                }

                foreach (var file in comparison.Modified)
                {
                    var fullPath = Path.Combine(newDir, file.RelativePath);
                    var patchPath = Path.Combine(tempDir, $"{file.NewHash}.patch");

                    if (file.IsPatch)
                    {
                        var entry = archive.CreateEntry($"patches/{file.NewHash}.patch", CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        using var fileStream = File.OpenRead(patchPath);
                        await fileStream.CopyToAsync(entryStream);
                    }
                    else
                    {
                        var entry = archive.CreateEntry(file.RelativePath, CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        using var fileStream = File.OpenRead(fullPath);
                        await fileStream.CopyToAsync(entryStream);
                    }
                }
            }

            if (_encryption != null)
            {
                var encryptedPath = outputPath + ".enc";
                await _encryption.EncryptFileAsync(outputPath, encryptedPath);
                File.Delete(outputPath);
                File.Move(encryptedPath, outputPath);
            }

            return outputPath;
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
