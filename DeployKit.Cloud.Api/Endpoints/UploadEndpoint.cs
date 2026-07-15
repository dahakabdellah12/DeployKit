using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Helpers;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public partial class UploadEndpoint
{
    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionRegex();

    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/upload", async (HttpRequest req, AppDbContext db, ILogger<UploadEndpoint> logger) =>
        {
            try
            {
                var user = AuditHelper.ResolveUser(req.HttpContext, db);
                if (user == null)
                    return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

                var key = req.Headers["X-Api-Key"].FirstOrDefault();
                var version = req.Query["version"].FirstOrDefault();
                var url = req.Query["url"].FirstOrDefault();
                var notes = req.Query["notes"].FirstOrDefault();
                var mandatoryStr = req.Query["mandatory"].FirstOrDefault();
                bool? mandatory = bool.TryParse(mandatoryStr, out var m) ? m : null;

                if (string.IsNullOrWhiteSpace(key))
                    return Results.BadRequest(new { error = "App key is required" });

                if (string.IsNullOrWhiteSpace(version) || !VersionRegex().IsMatch(version))
                    return Results.BadRequest(new { error = "Valid version (x.y.z) is required" });

                if (string.IsNullOrWhiteSpace(url) || (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")))
                    return Results.BadRequest(new { error = "Valid download URL is required" });

                var appReg = await db.Apps.FirstOrDefaultAsync(a => a.AppKey == key && a.UserId == user.Id);
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

                await AuditHelper.LogAsync(db, "upload", user.Username,
                    $"App: {appReg.AppName}, Version: {version}, Mandatory: {mandatory}",
                    req.HttpContext.Connection.RemoteIpAddress?.ToString());

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
