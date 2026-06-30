using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public static class CheckEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/check", async (string key, string v, AppDbContext db) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    return Results.BadRequest(new { error = "App key is required" });

                var appReg = await db.Apps
                    .Include(a => a.Packages)
                    .FirstOrDefaultAsync(a => a.AppKey == key);

                if (appReg == null)
                    return Results.NotFound(new { error = "App not found. Register first at POST /v1/register?name=YourApp" });

                var current = TryParseVersion(v);
                if (current == null)
                    return Results.BadRequest(new { error = "Invalid version format. Expected x.y.z" });

                var latest = appReg.Packages
                    .Where(p => TryParseVersion(p.Version) != null && TryParseVersion(p.Version) > current)
                    .OrderByDescending(p => TryParseVersion(p.Version))
                    .FirstOrDefault();

                if (latest == null)
                {
                    return Results.Ok(new UpdateResponse
                    {
                        HasUpdate = false,
                        LatestVersion = appReg.CurrentVersion
                    });
                }

                return Results.Ok(new UpdateResponse
                {
                    HasUpdate = true,
                    LatestVersion = latest.Version,
                    DownloadUrl = latest.DownloadUrl,
                    PackageSize = latest.FileSize,
                    ReleaseNotes = latest.ReleaseNotes,
                    IsMandatory = latest.IsMandatory
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });
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
