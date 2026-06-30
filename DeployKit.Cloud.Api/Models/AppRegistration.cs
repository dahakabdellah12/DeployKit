using System.ComponentModel.DataAnnotations;

namespace DeployKit.Cloud.Api.Models;

public class AppRegistration
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string AppKey { get; set; } = "";

    [MaxLength(64)]
    public string MandatoryAppKey { get; set; } = "";

    [Required, MaxLength(128)]
    public string AppName { get; set; } = "";

    [MaxLength(64)]
    public string CurrentVersion { get; set; } = "0.0.0";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<UpdatePackage> Packages { get; set; } = [];
}
