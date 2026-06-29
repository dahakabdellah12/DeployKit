using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DeployKit.Cloud.Api.Data;
using DeployKit.Cloud.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeployKit.Tests;

public class CloudApiIntegrationTests : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly string _storageDir;

    public CloudApiIntegrationTests()
    {
        _storageDir = Path.Combine(Path.GetTempPath(), "DeployKitTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storageDir);

        var dbPath = Path.Combine(Path.GetTempPath(), $"DeployKitTest_{Guid.NewGuid():N}.db");
        var connString = $"Data Source={dbPath}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connString));
        builder.Services.AddCors(opt =>
            opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        }

        app.UseCors();
        app.UseStaticFiles();

        RegisterEndpoint.Map(app);
        UploadEndpoint.Map(app, _storageDir);
        CheckEndpoint.Map(app);
        DownloadEndpoint.Map(app, _storageDir);
        AdminEndpoint.Map(app, _storageDir);

        app.StartAsync().GetAwaiter().GetResult();

        _host = app;
        _client = new HttpClient();
        _client.BaseAddress = new Uri(app.Urls.First());
    }

    [Fact]
    public async Task Register_CreatesApp()
    {
        var response = await _client.PostAsync("/v1/register?name=TestApp", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("appKey", out var key));
        Assert.Equal("TestApp", json.GetProperty("appName").GetString());
    }

    [Fact]
    public async Task Register_MissingName_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/v1/register?name=", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Check_UnknownKey_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/v1/check?key=invalid&v=1.0.0");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FullFlow_RegisterUploadCheckDownload()
    {
        var reg = await (await _client.PostAsync("/v1/register?name=FlowTest", null))
            .Content.ReadFromJsonAsync<JsonElement>();
        var appKey = reg.GetProperty("appKey").GetString()!;

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            zip.CreateEntry("test.txt").Open().Close();
        ms.Seek(0, SeekOrigin.Begin);

        var content = new ByteArrayContent(ms.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var upload = await (await _client.PostAsync(
            $"/v1/upload?key={appKey}&from=1.0.0&to=1.1.0&notes=Test+release", content))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("1.1.0", upload.GetProperty("toVersion").GetString());

        var check = await (await _client.GetAsync($"/v1/check?key={appKey}&v=1.0.0"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(check.GetProperty("hasUpdate").GetBoolean());

        var noUpdate = await (await _client.GetAsync($"/v1/check?key={appKey}&v=1.1.0"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(noUpdate.GetProperty("hasUpdate").GetBoolean());

        var pkgId = upload.GetProperty("id").GetInt32();
        var dlResponse = await _client.GetAsync($"/v1/dl/{pkgId}");
        Assert.Equal(HttpStatusCode.OK, dlResponse.StatusCode);
        Assert.Equal("application/zip", dlResponse.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task Upload_InvalidKey_ReturnsNotFound()
    {
        var content = new ByteArrayContent([]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var response = await _client.PostAsync(
            "/v1/upload?key=nonexistent&from=1.0.0&to=1.1.0", content);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Auth_Required()
    {
        var response = await _client.GetAsync("/v1/admin/apps");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
        try { Directory.Delete(_storageDir, true); } catch { }
    }
}
