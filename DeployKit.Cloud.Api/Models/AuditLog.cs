namespace DeployKit.Cloud.Api.Models;

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = "";
    public string? AdminUser { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
