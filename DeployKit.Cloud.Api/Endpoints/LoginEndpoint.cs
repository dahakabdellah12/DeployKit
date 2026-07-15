using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Helpers;
using DeployKit.Cloud.Api.Models;

namespace DeployKit.Cloud.Api.Endpoints;

public partial class LoginEndpoint
{
    [GeneratedRegex(@"^(?=.*[A-Z])(?=.*\d).{8,}$")]
    private static partial Regex PasswordRegex();

    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/admin/setup", async (SetupRequest req, HttpRequest httpReq, AppDbContext db, ILogger<LoginEndpoint> logger) =>
        {
            try
            {
                var err = ValidateCredentials(req.Username, req.Password);
                if (err != null) return Results.BadRequest(new { error = err });

                if (await db.Users.AnyAsync())
                    return Results.BadRequest(new { error = "Admin already exists" });

                var token = Guid.NewGuid().ToString("N");
                var user = new User
                {
                    Username = req.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                    Token = token,
                    TokenExpires = DateTime.UtcNow.AddDays(90)
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                await AuditHelper.LogAsync(db, "setup", req.Username, "Initial user setup", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { token, expiresAt = user.TokenExpires });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Setup failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        }).RequireRateLimiting("Login");

        app.MapPost("/v1/admin/users", async (SetupRequest req, HttpRequest httpReq, AppDbContext db, ILogger<LoginEndpoint> logger) =>
        {
            try
            {
                var err = ValidateCredentials(req.Username, req.Password);
                if (err != null) return Results.BadRequest(new { error = err });

                if (await db.Users.AnyAsync(u => u.Username == req.Username))
                    return Results.BadRequest(new { error = "Username already taken" });

                var token = Guid.NewGuid().ToString("N");
                var user = new User
                {
                    Username = req.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                    Token = token,
                    TokenExpires = DateTime.UtcNow.AddDays(90)
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                await AuditHelper.LogAsync(db, "register", req.Username, "New user registered", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { token, expiresAt = user.TokenExpires });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Registration failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        }).RequireRateLimiting("Login");

        app.MapPost("/v1/admin/login", async (LoginRequest req, HttpRequest httpReq, AppDbContext db, ILogger<LoginEndpoint> logger) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.BadRequest(new { error = "Username and password are required" });

                var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
                if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                {
                    await AuditHelper.LogAsync(db, "login_failed", req.Username, "Invalid credentials", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());
                    return Results.Json(new { error = "Invalid username or password" }, statusCode: 401);
                }

                user.Token = Guid.NewGuid().ToString("N");
                user.TokenExpires = DateTime.UtcNow.AddDays(90);
                await db.SaveChangesAsync();

                await AuditHelper.LogAsync(db, "login", req.Username, "Successful login", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { token = user.Token, expiresAt = user.TokenExpires });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        }).RequireRateLimiting("Login");

        app.MapPost("/v1/admin/logout", async (HttpRequest httpReq, AppDbContext db, ILogger<LoginEndpoint> logger) =>
        {
            try
            {
                var user = AuditHelper.ResolveUser(httpReq.HttpContext, db);
                if (user == null)
                    return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

                user.Token = Guid.NewGuid().ToString("N");
                await db.SaveChangesAsync();

                await AuditHelper.LogAsync(db, "logout", user.Username, "User logged out", httpReq.HttpContext.Connection.RemoteIpAddress?.ToString());

                return Results.Ok(new { logout = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Logout failed");
                return Results.Problem(detail: "Internal server error", statusCode: 500);
            }
        });

        app.MapGet("/v1/admin/status", async (AppDbContext db) =>
        {
            var hasAdmin = await db.Users.AnyAsync();
            return Results.Ok(new { hasAdmin, needsSetup = !hasAdmin });
        });
    }

    private static string? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return "Username and password are required";

        if (!PasswordRegex().IsMatch(password))
            return "Password must be at least 8 characters, include an uppercase letter and a digit";

        if (username.Length < 3)
            return "Username must be at least 3 characters";

        return null;
    }

    public record SetupRequest(string Username, string Password);
    public record LoginRequest(string Username, string Password);
}
