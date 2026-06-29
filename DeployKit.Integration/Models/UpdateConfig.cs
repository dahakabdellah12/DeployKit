namespace DeployKit.Integration.Models;

public class UpdateConfig
{
    public string AppKey { get; set; } = "";
    public string CurrentVersion { get; set; } = "";
    public string CloudUrl { get; set; } = "http://localhost:5000";
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(6);
    public bool AutoCheck { get; set; } = true;
}
