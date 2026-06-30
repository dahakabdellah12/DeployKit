using System.ComponentModel.DataAnnotations;

namespace DeployKit.Cloud.Api.Models;

public class UpdatePackage
{
    public int Id { get; set; }

    public int AppId { get; set; }

    [Required, MaxLength(16)]
    public string Version { get; set; } = "";

    [MaxLength(2048)]
    public string DownloadUrl { get; set; } = "";

    public long FileSize { get; set; }

    [MaxLength(2000)]
    public string ReleaseNotes { get; set; } = "";

    public bool IsMandatory { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppRegistration App { get; set; } = null!;
}
