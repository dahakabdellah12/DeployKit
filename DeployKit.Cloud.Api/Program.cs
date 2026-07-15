using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Resolve wwwroot from project or output directory
var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (!Directory.Exists(webRoot))
{
    var projectDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)));
    if (projectDir != null) webRoot = Path.Combine(projectDir, "wwwroot");
}
if (!Directory.Exists(webRoot))
    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
builder.Environment.WebRootPath = webRoot;

var dataDir = builder.Configuration["DataPath"] ?? "/data";
var connStr = builder.Configuration.GetConnectionString("Sqlite");
var connString = !string.IsNullOrWhiteSpace(connStr)
    ? connStr
    : $"Data Source={Path.Combine(dataDir, "deploykit.db")}";

// Resolve relative SQLite paths to the content root to avoid CWD issues
if (!string.IsNullOrWhiteSpace(connString) && connString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    var dbPath = connString["Data Source=".Length..].Trim();
    if (!Path.IsPathRooted(dbPath))
    {
        var rooted = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, dbPath));
        connString = $"Data Source={rooted}";
    }
}

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(connString));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddFixedWindowLimiter("Login", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("Upload", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("Api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("Admin", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseHsts();
app.UseRateLimiter();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "connect-src 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "form-action 'self'; " +
        "base-uri 'self'");
    ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.Append("X-Frame-Options", "DENY");
    ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    Directory.CreateDirectory(dataDir);
}

app.UseStaticFiles();

UploadEndpoint.Map(app);
CheckEndpoint.Map(app);
VersionEndpoint.Map(app);
AdminEndpoint.Map(app);
LoginEndpoint.Map(app);

app.MapFallbackToFile("index.html");

app.Run();
