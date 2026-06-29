using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public static class RegisterEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/register", async (string name, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "App name is required" });

            var appKey = Guid.NewGuid().ToString("N")[..12];
            db.Apps.Add(new AppRegistration
            {
                AppKey = appKey,
                AppName = name
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { appKey, appName = name });
        });
    }
}
