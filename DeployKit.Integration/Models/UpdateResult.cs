namespace DeployKit.Integration.Models;

public class UpdateResult
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public long PackageSize { get; set; }
    public string PackageHash { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public bool IsMandatory { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsSuccess => ErrorMessage == null;
}
