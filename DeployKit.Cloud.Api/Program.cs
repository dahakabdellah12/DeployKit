using Microsoft.EntityFrameworkCore;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var storagePath = Path.Combine(AppContext.BaseDirectory, "packages");
var connString = builder.Configuration.GetConnectionString("Sqlite")
    ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "deploykit.db")}";

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
}

app.UseCors();
RegisterEndpoint.Map(app);
UploadEndpoint.Map(app, storagePath);
CheckEndpoint.Map(app);
DownloadEndpoint.Map(app, storagePath);

app.Run();
