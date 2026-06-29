using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using DeployKit.Integration.Models;

namespace DeployKit.Integration.Services;

public class UpdateClient
{
    private readonly HttpClient _http;

    public UpdateClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<UpdateResult> CheckAsync(UpdateConfig config)
    {
        try
        {
            var url = $"{config.CloudUrl.TrimEnd('/')}/v1/check?key={config.AppKey}&v={config.CurrentVersion}";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new UpdateResult { ErrorMessage = $"Cloud API returned {(int)response.StatusCode}: {body}" };
            }

            var result = await response.Content.ReadFromJsonAsync<UpdateResult>();
            return result ?? new UpdateResult { ErrorMessage = "Empty response from cloud" };
        }
        catch (Exception ex)
        {
            return new UpdateResult { ErrorMessage = ex.Message };
        }
    }

    public async Task<string> DownloadPackageAsync(string downloadUrl, string destinationPath, IProgress<double>? progress = null)
    {
        using var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long bytesRead = 0;

        int read;
        while ((read = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            bytesRead += read;

            if (totalBytes > 0)
                progress?.Report((double)bytesRead / totalBytes * 100);
        }

        return destinationPath;
    }
}
