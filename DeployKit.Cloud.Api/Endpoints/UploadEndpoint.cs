using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public static class UploadEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/upload", async (string key, string version, string url,
            string? notes, bool? mandatory, AppDbContext db) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    return Results.BadRequest(new { error = "App key is required" });

                if (string.IsNullOrWhiteSpace(version))
                    return Results.BadRequest(new { error = "Version is required" });

                if (string.IsNullOrWhiteSpace(url))
                    return Results.BadRequest(new { error = "Download URL is required" });

                var appReg = await db.Apps.FirstOrDefaultAsync(a => a.AppKey == key);
                if (appReg == null)
                    return Results.NotFound(new { error = "Invalid app key" });

                var pkg = new UpdatePackage
                {
                    AppId = appReg.Id,
                    Version = version,
                    DownloadUrl = url,
                    ReleaseNotes = notes ?? "",
                    IsMandatory = mandatory ?? false
                };

                db.Packages.Add(pkg);
                appReg.CurrentVersion = version;
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    pkg.Id,
                    pkg.Version,
                    pkg.DownloadUrl,
                    pkg.IsMandatory
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });
    }
}
