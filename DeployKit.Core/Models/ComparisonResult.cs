namespace DeployKit.Core.Models;

public class ComparisonResult
{
    public List<ChangedFile> Added { get; set; } = [];
    public List<ChangedFile> Modified { get; set; } = [];
    public List<ChangedFile> Deleted { get; set; } = [];
    public List<ChangedFile> Unchanged { get; set; } = [];

    public int TotalChanged => Added.Count + Modified.Count + Deleted.Count;
    public long OldSize => Added.Sum(f => f.NewSize) + Modified.Sum(f => f.NewSize) + Unchanged.Sum(f => f.NewSize);
    public long NewSize => Added.Sum(f => f.NewSize) + Modified.Sum(f => f.NewSize) + Deleted.Sum(f => f.OldSize) + Unchanged.Sum(f => f.NewSize);
    public long FullPackageSize => Added.Sum(f => f.NewSize) + Modified.Sum(f => f.NewSize);
    public long PackageSize => Added.Sum(f => f.NewSize) + Modified.Sum(f => f.PatchSize > 0 ? f.PatchSize : f.NewSize);
}

public class ChangedFile
{
    public string RelativePath { get; set; } = string.Empty;
    public string OldHash { get; set; } = string.Empty;
    public string NewHash { get; set; } = string.Empty;
    public long OldSize { get; set; }
    public long NewSize { get; set; }
    public long PatchSize { get; set; }
    public bool IsPatch { get; set; }
    public FileChangeType ChangeType { get; set; }
}

public enum FileChangeType
{
    Added,
    Modified,
    Deleted,
    Unchanged
}
