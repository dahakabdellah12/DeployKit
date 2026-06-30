namespace DeployKit.Core.Models;

public class PackageManifest
{
    public int FormatVersion { get; set; } = 1;
    public string AppName { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public bool IsFullPackage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    private List<FileEntry> _added = [];
    private List<ModifiedEntry> _modified = [];
    private List<DeletedEntry> _deleted = [];
    private List<FileEntry> _allFiles = [];

    public List<FileEntry> Added { get => _added; set => _added = value ?? []; }
    public List<ModifiedEntry> Modified { get => _modified; set => _modified = value ?? []; }
    public List<DeletedEntry> Deleted { get => _deleted; set => _deleted = value ?? []; }
    public List<FileEntry> AllFiles { get => _allFiles; set => _allFiles = value ?? []; }
}

public class FileEntry
{
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long Size { get; set; }
}

public class ModifiedEntry
{
    public string Path { get; set; } = string.Empty;
    public string OldHash { get; set; } = string.Empty;
    public string NewHash { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsPatch { get; set; }
    public string? PatchFile { get; set; }
}

public class DeletedEntry
{
    public string Path { get; set; } = string.Empty;
}
