using System.ComponentModel.DataAnnotations;

namespace DeployKit.Cloud.Api.Models;

public class AdminUser
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Username { get; set; } = "";

    [Required, MaxLength(256)]
    public string PasswordHash { get; set; } = "";

    [Required, MaxLength(128)]
    public string Token { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime TokenExpires { get; set; } = DateTime.UtcNow.AddDays(30);
}
