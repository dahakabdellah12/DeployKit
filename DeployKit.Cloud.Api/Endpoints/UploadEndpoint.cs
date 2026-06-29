using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public static class UploadEndpoint
{
    public static void Map(WebApplication app, string storagePath)
    {
        app.MapPost("/v1/upload", async (HttpContext context,
            string key, string from, string to,
            string? notes, bool? mandatory, AppDbContext db) =>
        {
            try
            {
                var appReg = await db.Apps.FirstOrDefaultAsync(a => a.AppKey == key);
                if (appReg == null)
                    return Results.NotFound(new { error = "Invalid app key" });

                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                    return Results.BadRequest(new { error = "from and to parameters are required" });

                if (context.Request.ContentLength == 0 || context.Request.Body == null)
                    return Results.BadRequest(new { error = "Request body is empty" });

                var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var fileName = $"{appReg.Id}_{stamp}_update.zip";
                var filePath = Path.Combine(storagePath, fileName);

                Directory.CreateDirectory(storagePath);

                string fileHash;
                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await context.Request.Body.CopyToAsync(stream);
                    stream.Position = 0;
                    fileHash = Convert.ToHexString(SHA256.HashData(stream)).ToLower();
                }

                var fileSize = new FileInfo(filePath).Length;

                var pkg = new UpdatePackage
                {
                    AppId = appReg.Id,
                    FromVersion = from,
                    ToVersion = to,
                    StoredFileName = fileName,
                    FileSize = fileSize,
                    FileHash = fileHash,
                    ReleaseNotes = notes ?? "",
                    IsMandatory = mandatory ?? false
                };

                db.Packages.Add(pkg);
                appReg.CurrentVersion = to;
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    pkg.Id,
                    pkg.ToVersion,
                    pkg.FileSize
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });
    }
}
