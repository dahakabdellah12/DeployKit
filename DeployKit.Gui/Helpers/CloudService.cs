using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace DeployKit.Gui.Helpers;

public class CloudService
{
    private readonly HttpClient _http;

    public string? CloudUrl { get; set; }

    public CloudService(string? cloudUrl = null, string? adminToken = null)
    {
        CloudUrl = cloudUrl ?? "http://localhost:5000";
        _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(System.Threading.Timeout.Infinite) };
        if (!string.IsNullOrEmpty(adminToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    }

    public async Task<UploadResult> UploadAsync(string apiKey, string version, string downloadUrl,
        string? releaseNotes = null, bool? mandatory = null)
    {
        var baseUrl = CloudUrl ?? "http://localhost:5000";
        var url = $"{baseUrl.TrimEnd('/')}/v1/upload?version={Uri.EscapeDataString(version)}&url={Uri.EscapeDataString(downloadUrl)}";

        if (!string.IsNullOrEmpty(releaseNotes))
            url += $"&notes={Uri.EscapeDataString(releaseNotes)}";
        if (mandatory.HasValue)
            url += $"&mandatory={mandatory.Value}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Api-Key", apiKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UploadResult>() ?? new UploadResult();
    }

    public async Task<VersionCheckResult?> CheckVersionAsync(string apiKey)
    {
        var baseUrl = CloudUrl ?? "http://localhost:5000";
        var url = $"{baseUrl.TrimEnd('/')}/v1/version";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Api-Key", apiKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VersionCheckResult>();
    }
}

public class UploadResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";
}

public class VersionCheckResult
{
    [JsonPropertyName("hasUpdate")]
    public bool HasUpdate { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("isMandatory")]
    public bool IsMandatory { get; set; }
}
