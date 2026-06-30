using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public static class CheckEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/check", async (string key, string v, AppDbContext db, HttpContext ctx) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    return Results.BadRequest(new { error = "App key is required" });

                var appReg = await db.Apps
                    .Include(a => a.Packages)
                    .FirstOrDefaultAsync(a => a.AppKey == key || a.MandatoryAppKey == key);

                if (appReg == null)
                    return Results.NotFound(new { error = "App not found. Register first at POST /v1/register?name=YourApp" });

                var mandatoryOnly = key == appReg.MandatoryAppKey && !string.IsNullOrEmpty(appReg.MandatoryAppKey);

                var current = TryParseVersion(v);
                var scheme = ctx.Request.Scheme;
                var host = ctx.Request.Host.Value;

                var applicable = appReg.Packages
                    .Where(p =>
                    {
                        if (mandatoryOnly && !p.IsMandatory) return false;
                        var from = TryParseVersion(p.FromVersion);
                        var to = TryParseVersion(p.ToVersion);
                        if (from == null || to == null) return false;
                        return current != null && current >= from && current < to;
                    })
                    .OrderByDescending(p => TryParseVersion(p.ToVersion)?.Major)
                    .ThenByDescending(p => TryParseVersion(p.ToVersion)?.Minor)
                    .ThenByDescending(p => TryParseVersion(p.ToVersion)?.Build)
                    .ToList();

                var latest = applicable.FirstOrDefault();
                if (latest == null)
                {
                    return Results.Ok(new UpdateResponse
                    {
                        HasUpdate = false,
                        LatestVersion = appReg.CurrentVersion
                    });
                }

                var downloadUrl = $"{scheme}://{host}/v1/dl/{latest.Id}";

                return Results.Ok(new UpdateResponse
                {
                    HasUpdate = true,
                    LatestVersion = latest.ToVersion,
                    DownloadUrl = downloadUrl,
                    PackageSize = latest.FileSize,
                    PackageHash = latest.FileHash,
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
