using System.Security.Cryptography;
using System.Text;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Helpers;

public static class AuditHelper
{
    public static async Task LogAsync(AppDbContext db, string action, string? adminUser = null, string? details = null, string? ipAddress = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            AdminUser = adminUser ?? "",
            Details = details,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public static User? ResolveUser(HttpContext ctx, AppDbContext db)
    {
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ")) return null;
        var token = auth["Bearer ".Length..];
        var users = db.Users.Where(u => u.TokenExpires > DateTime.UtcNow).ToList();
        return users.FirstOrDefault(u =>
            u.Token.Length == token.Length &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(u.Token),
                Encoding.UTF8.GetBytes(token)));
    }
}
