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
                    .FirstOrDefaultAsync(a => a.AppKey == key);

                if (appReg == null)
                    return Results.NotFound(new { error = "App not found. Register first at POST /v1/register?name=YourApp" });

                var package = appReg.Packages
                    .Where(p => string.Compare(p.FromVersion, v, StringComparison.OrdinalIgnoreCase) == 0)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();

                if (package == null)
                {
                    return Results.Ok(new UpdateResponse
                    {
                        HasUpdate = false,
                        LatestVersion = appReg.CurrentVersion
                    });
                }

                var scheme = ctx.Request.Scheme;
                var host = ctx.Request.Host.Value;
                var downloadUrl = $"{scheme}://{host}/v1/dl/{package.Id}";

                return Results.Ok(new UpdateResponse
                {
                    HasUpdate = true,
                    LatestVersion = package.ToVersion,
                    DownloadUrl = downloadUrl,
                    PackageSize = package.FileSize,
                    PackageHash = package.FileHash,
                    ReleaseNotes = package.ReleaseNotes,
                    IsMandatory = package.IsMandatory
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });
    }
}
