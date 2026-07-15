using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public class UploadEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/upload", async (HttpRequest req, AppDbContext db, ILogger<UploadEndpoint> logger) =>
        {
            try
            {
                var auth = req.Headers.Authorization.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer "))
                    return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

                var token = auth["Bearer ".Length..];
                var users = await db.AdminUsers
                    .Where(u => u.TokenExpires > DateTime.UtcNow)
                    .ToListAsync();
                var admin = users.FirstOrDefault(u =>
                    u.Token.Length == token.Length &&
                    System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(u.Token),
                        System.Text.Encoding.UTF8.GetBytes(token)));

                if (admin == null)
                    return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

                var key = req.Headers["X-Api-Key"].FirstOrDefault();
                var version = req.Query["version"].FirstOrDefault();
                var url = req.Query["url"].FirstOrDefault();
                var notes = req.Query["notes"].FirstOrDefault();
                var mandatoryStr = req.Query["mandatory"].FirstOrDefault();
                bool? mandatory = bool.TryParse(mandatoryStr, out var m) ? m : null;

                if (string.IsNullOrWhiteSpace(key))
                    return Results.BadRequest(new { error = "App key is required" });

                if (string.IsNullOrWhiteSpace(version) || !System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"))
                    return Results.BadRequest(new { error = "Valid version (x.y.z) is required" });

                if (string.IsNullOrWhiteSpace(url) || (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")))
                    return Results.BadRequest(new { error = "Valid download URL is required" });

                var appReg = await db.Apps.FirstOrDefaultAsync(a => a.AppKey == key);
                if (appReg == null)
                    return Results.NotFound(new { error = "Invalid app key" });

                var oldPackages = await db.Packages.Where(p => p.AppId == appReg.Id).ToListAsync();
                db.Packages.RemoveRange(oldPackages);

                var pkg = new UpdatePackage
                {
                    AppId = appReg.Id,
                    Version = version,
                    DownloadUrl = url,
                    ReleaseNotes = notes ?? "",
                    IsMandatory = mandatory ?? false,
                    IsFullPackage = true
                };

                db.Packages.Add(pkg);
                appReg.CurrentVersion = version;
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    pkg.Id,
                    pkg.Version,
                    pkg.DownloadUrl,
                    pkg.IsMandatory,
                    pkg.IsFullPackage
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Upload failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        }).RequireRateLimiting("Upload");
    }
}
