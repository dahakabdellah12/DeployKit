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

    public async Task<UploadResult> UploadAsync(string apiKey, string version, string downloadUrl,
        string? releaseNotes = null, bool? mandatory = null)
    {
        var baseUrl = CloudUrl ?? "http://localhost:5000";
        var url = $"{baseUrl.TrimEnd('/')}/v1/upload?key={apiKey}&version={Uri.EscapeDataString(version)}&url={Uri.EscapeDataString(downloadUrl)}";

        if (!string.IsNullOrEmpty(releaseNotes))
            url += $"&notes={Uri.EscapeDataString(releaseNotes)}";
        if (mandatory.HasValue)
            url += $"&mandatory={mandatory.Value}";

        var response = await _http.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UploadResult>() ?? new UploadResult();
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
