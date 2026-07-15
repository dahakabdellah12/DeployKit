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
            AdminUser = adminUser,
            Details = details,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public static string? GetAdminUsername(HttpContext ctx, AppDbContext db)
    {
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ")) return null;
        var token = auth["Bearer ".Length..];
        var user = db.AdminUsers.FirstOrDefault(u => u.Token == token && u.TokenExpires > DateTime.UtcNow);
        return user?.Username;
    }
}
