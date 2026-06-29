using System.IO;
using System.Reflection;
using DeployKit.Integration.Models;
using DeployKit.Integration.Services;

namespace DeployKit.Integration;

public static class DeployKit
{
    private static UpdateConfig? _config;
    private static UpdateClient? _client;
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeployKit", "sdk_config.json");

    public static void Configure(string appKey, string? cloudUrl = null)
    {
        _config = new UpdateConfig
        {
            AppKey = appKey,
            CloudUrl = cloudUrl ?? "https://api.deploykit.app",
            CurrentVersion = DetectVersion()
        };

        _client = new UpdateClient();

        if (_config.AutoCheck)
            _ = CheckAndNotifyAsync();
    }

    private static string DetectVersion()
    {
        try
        {
            var ver = Assembly.GetEntryAssembly()?.GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    public static async Task<UpdateResult> CheckAsync()
    {
        if (_config == null || _client == null)
            return new UpdateResult { ErrorMessage = "SDK not configured. Call DeployKit.Configure(apiKey) first." };

        return await _client.CheckAsync(_config);
    }

    public static async Task<bool> DownloadAndApplyAsync(UpdateResult result, string downloadDir)
    {
        if (_client == null) return false;

        Directory.CreateDirectory(downloadDir);
        var zipPath = Path.Combine(downloadDir, $"update_{Guid.NewGuid():N}.zip");

        await _client.DownloadPackageAsync(result.DownloadUrl, zipPath);
        return true;
    }

    private static async Task CheckAndNotifyAsync()
    {
        try
        {
            var result = await CheckAsync();
            if (result.HasUpdate)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var window = new UI.UpdateWindow(result, _config!);
                    window.ShowDialog();
                });
            }
        }
        catch
        {
            // Silent fail - don't crash the app
        }
    }
}
