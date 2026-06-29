using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DeployKit.Gui.Helpers;

public class CloudService
{
    private readonly HttpClient _http;

    public string? CloudUrl { get; set; }

    public CloudService(string? cloudUrl = null)
    {
        CloudUrl = cloudUrl ?? "http://localhost:5000";
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<RegisterResult> RegisterAsync(string appName)
    {
        var baseUrl = CloudUrl ?? "http://localhost:5000";
        var url = $"{baseUrl.TrimEnd('/')}/v1/register?name={Uri.EscapeDataString(appName)}";
        var response = await _http.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RegisterResult>() ?? new RegisterResult();
    }

    public async Task<UploadResult> UploadAsync(string apiKey, string fromVersion, string toVersion,
        string filePath, string? releaseNotes = null, bool? mandatory = null)
    {
        var baseUrl = CloudUrl ?? "http://localhost:5000";
        var url = $"{baseUrl.TrimEnd('/')}/v1/upload?key={apiKey}&from={fromVersion}&to={toVersion}";

        if (!string.IsNullOrEmpty(releaseNotes))
            url += $"&notes={Uri.EscapeDataString(releaseNotes)}";
        if (mandatory.HasValue)
            url += $"&mandatory={mandatory.Value}";

        using var fileStream = File.OpenRead(filePath);
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UploadResult>() ?? new UploadResult();
    }
}

public class RegisterResult
{
    [JsonPropertyName("appKey")]
    public string AppKey { get; set; } = "";

    [JsonPropertyName("appName")]
    public string AppName { get; set; } = "";
}

public class UploadResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("toVersion")]
    public string ToVersion { get; set; } = "";

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }
}
