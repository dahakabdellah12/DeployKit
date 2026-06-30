using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public record RegisterRequest(string AppName);
public record UpdatePackageRequest(string? Version, string? DownloadUrl, string? ReleaseNotes, bool? IsMandatory);

public static class AdminEndpoint
{
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/v1/admin");
        admin.AddEndpointFilter(async (ctx, next) =>
        {
            var auth = ctx.HttpContext.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer "))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

            var token = auth["Bearer ".Length..];
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Token == token && u.TokenExpires > DateTime.UtcNow);

            if (user == null)
                return Results.Json(new { error = "Invalid or expired token" }, statusCode: 401);

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

        admin.MapPost("/register", async (RegisterRequest req, AppDbContext db) =>
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
                return Results.Ok(new { appKey, appName = req.AppName });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        admin.MapPost("/apps/{key}/regenerate-key", async (string key, AppDbContext db) =>
        {
            var app = await db.Apps.FirstOrDefaultAsync(a => a.AppKey == key);
            if (app is null)
                return Results.NotFound(new { error = "App not found" });

            var newKey = Guid.NewGuid().ToString("N");
            app.AppKey = newKey;
            await db.SaveChangesAsync();

            return Results.Ok(new { appKey = newKey });
        });

        admin.MapDelete("/apps/{key}", async (string key, AppDbContext db) =>
        {
            var app = await db.Apps
                .Include(a => a.Packages)
                .FirstOrDefaultAsync(a => a.AppKey == key);

            if (app is null)
                return Results.NotFound(new { error = "App not found" });

            db.Packages.RemoveRange(app.Packages);
            db.Apps.Remove(app);
            await db.SaveChangesAsync();

            return Results.Ok(new { deleted = true });
        });

        admin.MapPatch("/packages/{id:int}", async (int id, UpdatePackageRequest req, AppDbContext db) =>
        {
            var pkg = await db.Packages.Include(p => p.App).FirstOrDefaultAsync(p => p.Id == id);
            if (pkg is null)
                return Results.NotFound(new { error = "Package not found" });

            if (req.Version is not null)
            {
                if (string.IsNullOrWhiteSpace(req.Version))
                    return Results.BadRequest(new { error = "Version cannot be empty" });
                pkg.Version = req.Version;
                pkg.App.CurrentVersion = req.Version;
            }

            if (req.DownloadUrl is not null)
            {
                if (string.IsNullOrWhiteSpace(req.DownloadUrl))
                    return Results.BadRequest(new { error = "Download URL cannot be empty" });
                pkg.DownloadUrl = req.DownloadUrl;
            }

            if (req.ReleaseNotes is not null)
                pkg.ReleaseNotes = req.ReleaseNotes;

            if (req.IsMandatory is not null)
                pkg.IsMandatory = req.IsMandatory.Value;

            await db.SaveChangesAsync();

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
        });

        admin.MapDelete("/packages/{id:int}", async (int id, AppDbContext db) =>
        {
            var pkg = await db.Packages.FindAsync(id);
            if (pkg is null)
                return Results.NotFound(new { error = "Package not found" });

            db.Packages.Remove(pkg);
            await db.SaveChangesAsync();

            return Results.Ok(new { deleted = true });
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
