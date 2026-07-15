using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Helpers;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public class LoginEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/admin/setup", async (SetupRequest req, HttpRequest httpReq, AppDbContext db, ILogger<LoginEndpoint> logger) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.BadRequest(new { error = "Username and password are required" });

                if (req.Password.Length < 6)
                    return Results.BadRequest(new { error = "Password must be at least 6 characters" });

                if (await db.AdminUsers.AnyAsync())
                    return Results.BadRequest(new { error = "Admin already exists" });

                var token = Guid.NewGuid().ToString("N");
                var user = new AdminUser
                {
                    Username = req.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                    Token = token,
                    TokenExpires = DateTime.UtcNow.AddDays(90)
                };

                db.AdminUsers.Add(user);
                await db.SaveChangesAsync();

                await AuditHelper.LogAsync(db, "setup", req.Username, "Initial admin setup", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { token, expiresAt = user.TokenExpires });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Admin setup failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        }).RequireRateLimiting("Login");

        app.MapPost("/v1/admin/login", async (LoginRequest req, HttpRequest httpReq, AppDbContext db, ILogger<LoginEndpoint> logger) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.BadRequest(new { error = "Username and password are required" });

                var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == req.Username);
                if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                {
                    await AuditHelper.LogAsync(db, "login_failed", req.Username, "Invalid credentials", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());
                    return Results.Json(new { error = "Invalid username or password" }, statusCode: 401);
                }

                if (user.TokenExpires < DateTime.UtcNow)
                {
                    user.Token = Guid.NewGuid().ToString("N");
                    user.TokenExpires = DateTime.UtcNow.AddDays(90);
                }

                user.TokenExpires = DateTime.UtcNow.AddDays(90);
                await db.SaveChangesAsync();

                await AuditHelper.LogAsync(db, "login", req.Username, "Successful login", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { token = user.Token, expiresAt = user.TokenExpires });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Admin login failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        }).RequireRateLimiting("Login");

        app.MapGet("/v1/admin/status", async (AppDbContext db) =>
        {
            var hasAdmin = await db.AdminUsers.AnyAsync();
            return Results.Ok(new { hasAdmin, needsSetup = !hasAdmin });
        });
    }

    public record SetupRequest(string Username, string Password);
    public record LoginRequest(string Username, string Password);
}
