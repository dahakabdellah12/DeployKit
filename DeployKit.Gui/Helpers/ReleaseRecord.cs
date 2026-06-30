namespace DeployKit.Gui.Helpers;

public class ReleaseRecord
{
    public string AppName { get; set; } = "";
    public string FromVersion { get; set; } = "";
    public string ToVersion { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string ZipPath { get; set; } = "";
    public long FileSize { get; set; }
    public string CloudUrl { get; set; } = "";
    public bool IsRegisteredInCloud { get; set; }
    public bool CreatedGitHubRelease { get; set; }
    public string GitHubReleaseUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
