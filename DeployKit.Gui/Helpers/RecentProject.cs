namespace DeployKit.Gui.Helpers;

public class RecentProject
{
    public string Name { get; set; } = "";
    public string FromVersion { get; set; } = "";
    public string ToVersion { get; set; } = "";
    public string OldDir { get; set; } = "";
    public string NewDir { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public DateTime LastUsed { get; set; }
}
