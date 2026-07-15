using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Helpers;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public record RegisterRequest(string AppName);
public record UpdatePackageRequest(string? Version, string? DownloadUrl, string? ReleaseNotes, bool? IsMandatory);

public class AdminEndpoint
{
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/v1/admin").RequireRateLimiting("Admin");
        admin.AddEndpointFilter(async (ctx, next) =>
        {
            var auth = ctx.HttpContext.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer "))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

            var token = auth["Bearer ".Length..];
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var users = await db.AdminUsers
                .Where(u => u.TokenExpires > DateTime.UtcNow)
                .ToListAsync();
            var user = users.FirstOrDefault(u =>
                u.Token.Length == token.Length &&
                CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(u.Token),
                    System.Text.Encoding.UTF8.GetBytes(token)));

            if (user == null)
                return Results.Json(new { error = "Invalid or expired token" }, statusCode: 401);

            ctx.HttpContext.Items["AdminUser"] = user.Username;
            return await next(ctx);
        });

        admin.MapGet("/apps", async (AppDbContext db) =>
        {
            var apps = await db.Apps
                .Include(a => a.Packages)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.AppKey,
                    a.AppName,
                    a.CurrentVersion,
                    a.CreatedAt,
                    PackageCount = a.Packages.Count
                })
                .ToListAsync();

            return Results.Ok(apps);
        });

        admin.MapGet("/apps/{key}", async (string key, AppDbContext db) =>
        {
            var app = await db.Apps
                .Include(a => a.Packages.OrderByDescending(p => p.CreatedAt))
                .FirstOrDefaultAsync(a => a.AppKey == key);

            if (app is null)
                return Results.NotFound(new { error = "App not found" });

            return Results.Ok(new
            {
                app.Id,
                app.AppKey,
                app.AppName,
                app.CurrentVersion,
                app.CreatedAt,
                Packages = app.Packages.Select(p => new
                {
                    p.Id,
                    p.Version,
                    p.DownloadUrl,
                    p.FileSize,
                    p.ReleaseNotes,
                    p.IsMandatory,
                    p.IsFullPackage,
                    p.CreatedAt
                })
            });
        });

        admin.MapPost("/register", async (RegisterRequest req, HttpRequest httpReq, AppDbContext db, ILogger<AdminEndpoint> logger) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.AppName))
                    return Results.BadRequest(new { error = "App name is required" });

                var appKey = Guid.NewGuid().ToString("N");
                db.Apps.Add(new AppRegistration
                {
                    AppKey = appKey,
                    AppName = req.AppName
                });

                await db.SaveChangesAsync();

                var admin = httpReq.HttpContext.Items["AdminUser"] as string;
                await AuditHelper.LogAsync(db, "register_app", admin, $"App: {req.AppName}, Key: {appKey}", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { appKey, appName = req.AppName });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "App registration failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        });

        admin.MapPost("/apps/{key}/regenerate-key", async (string key, HttpRequest httpReq, AppDbContext db, ILogger<AdminEndpoint> logger) =>
        {
            try
            {
                var app = await db.Apps.FirstOrDefaultAsync(a => a.AppKey == key);
                if (app is null)
                    return Results.NotFound(new { error = "App not found" });

                var newKey = Guid.NewGuid().ToString("N");
                app.AppKey = newKey;
                await db.SaveChangesAsync();

                var adminUser = httpReq.HttpContext.Items["AdminUser"] as string;
                await AuditHelper.LogAsync(db, "regenerate_key", adminUser, $"App: {app.AppName}, Old: {key}, New: {newKey}", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { appKey = newKey });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Regenerate key failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        });

        admin.MapDelete("/apps/{key}", async (string key, HttpRequest httpReq, AppDbContext db, ILogger<AdminEndpoint> logger) =>
        {
            try
            {
                var app = await db.Apps
                    .Include(a => a.Packages)
                    .FirstOrDefaultAsync(a => a.AppKey == key);

                if (app is null)
                    return Results.NotFound(new { error = "App not found" });

                var appName = app.AppName;
                db.Packages.RemoveRange(app.Packages);
                db.Apps.Remove(app);
                await db.SaveChangesAsync();

                var admin = httpReq.HttpContext.Items["AdminUser"] as string;
                await AuditHelper.LogAsync(db, "delete_app", admin, $"App: {appName}", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { deleted = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Delete app failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        });

        admin.MapPatch("/packages/{id:int}", async (int id, UpdatePackageRequest req, HttpRequest httpReq, AppDbContext db, ILogger<AdminEndpoint> logger) =>
        {
            try
            {
                var pkg = await db.Packages.Include(p => p.App).FirstOrDefaultAsync(p => p.Id == id);
                if (pkg is null)
                    return Results.NotFound(new { error = "Package not found" });

                var changes = new System.Text.StringBuilder();
                if (req.Version is not null)
                {
                    if (string.IsNullOrWhiteSpace(req.Version))
                        return Results.BadRequest(new { error = "Version cannot be empty" });
                    changes.Append($"Version: {pkg.Version} -> {req.Version}; ");
                    pkg.Version = req.Version;
                    pkg.App.CurrentVersion = req.Version;
                }

                if (req.DownloadUrl is not null)
                {
                    if (string.IsNullOrWhiteSpace(req.DownloadUrl))
                        return Results.BadRequest(new { error = "Download URL cannot be empty" });
                    changes.Append($"URL changed; ");
                    pkg.DownloadUrl = req.DownloadUrl;
                }

                if (req.ReleaseNotes is not null)
                {
                    changes.Append("ReleaseNotes updated; ");
                    pkg.ReleaseNotes = req.ReleaseNotes;
                }

                if (req.IsMandatory is not null)
                {
                    changes.Append($"Mandatory: {pkg.IsMandatory} -> {req.IsMandatory}; ");
                    pkg.IsMandatory = req.IsMandatory.Value;
                }

                await db.SaveChangesAsync();

                var admin = httpReq.HttpContext.Items["AdminUser"] as string;
                await AuditHelper.LogAsync(db, "update_package", admin, $"Package {id}, App: {pkg.App.AppName}, {changes}", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new
                {
                    pkg.Id,
                    pkg.Version,
                    pkg.DownloadUrl,
                    pkg.ReleaseNotes,
                    pkg.IsMandatory,
                    pkg.IsFullPackage,
                    pkg.CreatedAt
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Update package failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        });

        admin.MapDelete("/packages/{id:int}", async (int id, HttpRequest httpReq, AppDbContext db, ILogger<AdminEndpoint> logger) =>
        {
            try
            {
                var pkg = await db.Packages.Include(p => p.App).FirstOrDefaultAsync(p => p.Id == id);
                if (pkg is null)
                    return Results.NotFound(new { error = "Package not found" });

                var appName = pkg.App?.AppName;
                db.Packages.Remove(pkg);
                await db.SaveChangesAsync();

                var admin = httpReq.HttpContext.Items["AdminUser"] as string;
                await AuditHelper.LogAsync(db, "delete_package", admin, $"Package {id}, App: {appName}, Version: {pkg.Version}", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { deleted = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Delete package failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        });

        admin.MapGet("/packages/{id:int}", async (int id, AppDbContext db) =>
        {
            var pkg = await db.Packages
                .Include(p => p.App)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pkg is null)
                return Results.NotFound(new { error = "Package not found" });

            return Results.Ok(new
            {
                pkg.Id,
                pkg.Version,
                pkg.DownloadUrl,
                pkg.FileSize,
                pkg.ReleaseNotes,
                pkg.IsMandatory,
                pkg.IsFullPackage,
                pkg.CreatedAt,
                App = new { pkg.App.AppName, pkg.App.AppKey }
            });
        });
    }
}
