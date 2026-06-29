namespace DeployKit.Cloud.Api.Models;

public class UpdateResponse
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public long PackageSize { get; set; }
    public string PackageHash { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public bool IsMandatory { get; set; }
}
