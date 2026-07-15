using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;

namespace DeployKit.Cloud.Api.Endpoints;

public class CheckResponse
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public long PackageSize { get; set; }
    public string ReleaseNotes { get; set; } = "";
    public bool IsMandatory { get; set; }
    public bool IsFullPackage { get; set; } = true;
}

public class CheckEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/check", async (HttpRequest req, AppDbContext db, ILogger<CheckEndpoint> logger) =>
        {
            try
            {
                var key = req.Headers["X-Api-Key"].FirstOrDefault();
                var v = req.Query["v"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(key))
                    return Results.BadRequest(new { error = "App key is required (X-Api-Key header)" });

                var appReg = await db.Apps
                    .Include(a => a.Packages)
                    .FirstOrDefaultAsync(a => a.AppKey == key);

                if (appReg == null)
                    return Results.NotFound(new { error = "App not found" });

                var current = TryParseVersion(v!);
                if (current == null)
                    return Results.BadRequest(new { error = "Invalid version format. Expected x.y.z" });

                var latest = appReg.Packages
                    .Where(p =>
                    {
                        var pkgVer = TryParseVersion(p.Version);
                        return pkgVer != null && pkgVer > current;
                    })
                    .OrderByDescending(p => TryParseVersion(p.Version))
                    .FirstOrDefault();

                if (latest == null)
                {
                    return Results.Ok(new CheckResponse
                    {
                        HasUpdate = false,
                        LatestVersion = appReg.CurrentVersion
                    });
                }

                return Results.Ok(new CheckResponse
                {
                    HasUpdate = true,
                    LatestVersion = latest.Version,
                    DownloadUrl = latest.DownloadUrl,
                    PackageSize = latest.FileSize,
                    ReleaseNotes = latest.ReleaseNotes,
                    IsMandatory = latest.IsMandatory,
                    IsFullPackage = true
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Check failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        }).RequireRateLimiting("Api");
    }

    private static Version? TryParseVersion(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var parts = v.Trim().Split('.');
        if (parts.Length < 2) return null;
        if (!int.TryParse(parts[0], out var major)) return null;
        if (!int.TryParse(parts[1], out var minor)) return null;
        var build = parts.Length > 2 && int.TryParse(parts[2], out var b) ? b : 0;
        var rev = parts.Length > 3 && int.TryParse(parts[3], out var r) ? r : 0;
        return new Version(major, minor, Math.Max(build, 0), Math.Max(rev, 0));
    }
}
