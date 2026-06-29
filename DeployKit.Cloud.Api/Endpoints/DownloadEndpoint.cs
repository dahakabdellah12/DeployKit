using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;

namespace DeployKit.Cloud.Api.Endpoints;

public static class DownloadEndpoint
{
    public static void Map(WebApplication app, string storagePath)
    {
        app.MapGet("/v1/dl/{packageId:int}", async (int packageId, AppDbContext db) =>
        {
            try
            {
                var pkg = await db.Packages.FindAsync(packageId);
                if (pkg == null)
                    return Results.NotFound(new { error = "Package not found" });

                var filePath = Path.Combine(storagePath, pkg.StoredFileName);
                if (!File.Exists(filePath))
                    return Results.NotFound(new { error = "Package file not found on disk" });

                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return Results.File(stream, "application/zip", $"update_{pkg.FromVersion}_to_{pkg.ToVersion}.zip");
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });
    }
}
