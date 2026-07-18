using System.ComponentModel.DataAnnotations;

namespace DeployKit.Cloud.Api.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Username { get; set; } = "";

    [Required, MaxLength(256)]
    public string PasswordHash { get; set; } = "";

    [Required, MaxLength(128)]
    public string Token { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime TokenExpires { get; set; } = DateTime.UtcNow.AddHours(8);

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockedUntilUtc { get; set; }
}
