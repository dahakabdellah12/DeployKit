using System.IO.Compression;
using System.Text.Json;
using DeployKit.Core.Models;

namespace DeployKit.Core.Services;

public class PackageBuilder
{
    private readonly HashService _hashService = new();
    private readonly EncryptionService? _encryption;

    public PackageBuilder(EncryptionService? encryption = null)
    {
        _encryption = encryption;
    }

    public async Task<string> BuildFullPackageAsync(
        string newDir, string appName, string version, string outputPath)
    {
        if (!Directory.Exists(newDir))
            throw new InvalidOperationException($"Source directory not found: {newDir}");

        var dir = Path.GetDirectoryName(outputPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        var files = Directory.GetFiles(newDir, "*", SearchOption.AllDirectories);

        var fileEntries = new List<FileEntry>();
        foreach (var file in files)
        {
            var relPath = Path.GetRelativePath(newDir, file);
            var fileInfo = new FileInfo(file);
            var hash = await _hashService.ComputeHashAsync(file);
            fileEntries.Add(new FileEntry
            {
                Path = relPath,
                Hash = hash,
                Size = fileInfo.Length
            });
        }

        var manifest = new PackageManifest
        {
            AppName = appName,
            TargetVersion = version,
            IsFullPackage = true,
            AllFiles = fileEntries
        };

        using (var stream = File.Create(outputPath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                await writer.WriteAsync(json);
            }

            foreach (var entry in fileEntries)
            {
                var fullPath = Path.Combine(newDir, entry.Path);
                var zipEntry = archive.CreateEntry(entry.Path, CompressionLevel.Optimal);
                using var entryStream = zipEntry.Open();
                using var fileStream = File.OpenRead(fullPath);
                await fileStream.CopyToAsync(entryStream);
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
}
