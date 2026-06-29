using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var storagePath = Path.Combine(AppContext.BaseDirectory, "packages");
var connStr = builder.Configuration.GetConnectionString("Sqlite");
var connString = !string.IsNullOrWhiteSpace(connStr)
    ? connStr
    : $"Data Source={Path.Combine(AppContext.BaseDirectory, "deploykit.db")}";

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
    Directory.CreateDirectory(storagePath);
}

app.UseCors();
app.UseStaticFiles();

RegisterEndpoint.Map(app);
UploadEndpoint.Map(app, storagePath);
CheckEndpoint.Map(app);
DownloadEndpoint.Map(app, storagePath);
AdminEndpoint.Map(app, storagePath);

app.MapFallbackToFile("index.html");

app.Run();
