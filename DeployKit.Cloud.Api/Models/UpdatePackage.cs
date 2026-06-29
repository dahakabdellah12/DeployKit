using System.ComponentModel.DataAnnotations;

namespace DeployKit.Cloud.Api.Models;

public class UpdatePackage
{
    public int Id { get; set; }

    public int AppId { get; set; }

    [Required, MaxLength(16)]
    public string FromVersion { get; set; } = "";

    [Required, MaxLength(16)]
    public string ToVersion { get; set; } = "";

    [MaxLength(256)]
    public string StoredFileName { get; set; } = "";

    public long FileSize { get; set; }

    [MaxLength(64)]
    public string FileHash { get; set; } = "";

    [MaxLength(2000)]
    public string ReleaseNotes { get; set; } = "";

    public bool IsMandatory { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppRegistration App { get; set; } = null!;
}
