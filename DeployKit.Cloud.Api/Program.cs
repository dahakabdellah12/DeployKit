using Microsoft.EntityFrameworkCore;
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

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(connString));

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    Directory.CreateDirectory(dataDir);
}

app.UseCors();
app.UseStaticFiles();

RegisterEndpoint.Map(app);
UploadEndpoint.Map(app);
CheckEndpoint.Map(app);
AdminEndpoint.Map(app);
LoginEndpoint.Map(app);

app.MapFallbackToFile("index.html");

app.Run();
