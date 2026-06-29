using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public record RegisterRequest(string AppName);

public static class AdminEndpoint
{
    public static void Map(WebApplication app, string storagePath)
    {
        var adminKey = app.Configuration["AdminKey"] ?? "admin";

        var admin = app.MapGroup("/v1/admin");
        admin.AddEndpointFilter(async (ctx, next) =>
        {
            var key = ctx.HttpContext.Request.Headers["X-Admin-Key"].FirstOrDefault();
            if (key != adminKey)
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            return await next(ctx);
        });

        admin.MapGet("/apps", async (AppDbContext db, HttpRequest req) =>
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
                    PackageCount = a.Packages.Count,
                    TotalDownloads = 0
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
                    p.FromVersion,
                    p.ToVersion,
                    p.FileSize,
                    p.ReleaseNotes,
                    p.IsMandatory,
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

                var appKey = Guid.NewGuid().ToString("N")[..12];
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

        admin.MapDelete("/apps/{key}", async (string key, AppDbContext db) =>
        {
            var app = await db.Apps
                .Include(a => a.Packages)
                .FirstOrDefaultAsync(a => a.AppKey == key);

            if (app is null)
                return Results.NotFound(new { error = "App not found" });

            foreach (var pkg in app.Packages)
            {
                var filePath = Path.Combine(storagePath, pkg.StoredFileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            db.Packages.RemoveRange(app.Packages);
            db.Apps.Remove(app);
            await db.SaveChangesAsync();

            return Results.Ok(new { deleted = true });
        });

        admin.MapDelete("/packages/{id:int}", async (int id, AppDbContext db) =>
        {
            var pkg = await db.Packages.FindAsync(id);
            if (pkg is null)
                return Results.NotFound(new { error = "Package not found" });

            var filePath = Path.Combine(storagePath, pkg.StoredFileName);
            if (File.Exists(filePath))
                File.Delete(filePath);

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
                pkg.FromVersion,
                pkg.ToVersion,
                pkg.FileSize,
                pkg.ReleaseNotes,
                pkg.IsMandatory,
                pkg.CreatedAt,
                App = new { pkg.App.AppName, pkg.App.AppKey }
            });
        });
    }
}
