using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public static class LoginEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/admin/setup", async (SetupRequest req, AppDbContext db) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.BadRequest(new { error = "Username and password are required" });

                if (await db.AdminUsers.AnyAsync())
                    return Results.BadRequest(new { error = "Admin already exists" });

                var token = Guid.NewGuid().ToString("N");
                var user = new AdminUser
                {
                    Username = req.Username,
                    PasswordHash = HashPassword(req.Password),
                    Token = token,
                    TokenExpires = DateTime.UtcNow.AddDays(90)
                };

                db.AdminUsers.Add(user);
                await db.SaveChangesAsync();

                return Results.Ok(new { token, expiresAt = user.TokenExpires });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        app.MapPost("/v1/admin/login", async (LoginRequest req, AppDbContext db) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.BadRequest(new { error = "Username and password are required" });

                var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == req.Username);
                if (user == null || user.PasswordHash != HashPassword(req.Password))
                    return Results.Unauthorized();

                if (user.TokenExpires < DateTime.UtcNow)
                {
                    user.Token = Guid.NewGuid().ToString("N");
                    user.TokenExpires = DateTime.UtcNow.AddDays(90);
                }

                user.TokenExpires = DateTime.UtcNow.AddDays(90);
                await db.SaveChangesAsync();

                return Results.Ok(new { token = user.Token, expiresAt = user.TokenExpires });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        app.MapGet("/v1/admin/status", async (AppDbContext db) =>
        {
            var hasAdmin = await db.AdminUsers.AnyAsync();
            return Results.Ok(new { hasAdmin, needsSetup = !hasAdmin });
        });
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public record SetupRequest(string Username, string Password);
    public record LoginRequest(string Username, string Password);
}
