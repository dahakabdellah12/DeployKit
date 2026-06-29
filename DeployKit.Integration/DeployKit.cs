using System.IO;
using System.Reflection;
using System.Text.Json;
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
    private static readonly string RollbackInfoPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeployKit", "rollback.json");

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

    public static RollbackInfo? GetRollbackInfo()
    {
        try
        {
            if (!File.Exists(RollbackInfoPath))
                return null;

            var json = File.ReadAllText(RollbackInfoPath);
            return JsonSerializer.Deserialize<RollbackInfo>(json);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> RollbackAsync()
    {
        var info = GetRollbackInfo();
        if (info == null)
            return "No rollback backup found.";

        if (!Directory.Exists(info.BackupPath))
            return $"Rollback backup directory not found: {info.BackupPath}";

        try
        {
            var rollback = new Core.Services.RollbackService(
                Path.GetDirectoryName(info.BackupPath)!);

            rollback.Restore(info.BackupPath, info.TargetDir);
            rollback.Cleanup(info.BackupPath);

            if (File.Exists(RollbackInfoPath))
                File.Delete(RollbackInfoPath);

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"Rollback failed: {ex.Message}";
        }
    }

    internal static void SaveRollbackInfo(string backupPath, string targetDir, string previousVersion)
    {
        try
        {
            var dir = Path.GetDirectoryName(RollbackInfoPath)!;
            Directory.CreateDirectory(dir);

            var info = new RollbackInfo
            {
                BackupPath = backupPath,
                TargetDir = targetDir,
                PreviousVersion = previousVersion,
                CreatedAt = DateTime.UtcNow
            };

            File.WriteAllText(RollbackInfoPath, JsonSerializer.Serialize(info));
        }
        catch
        {
        }
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeployKit] Auto-check failed: {ex.Message}");
        }
    }
}

public class RollbackInfo
{
    public string BackupPath { get; set; } = "";
    public string TargetDir { get; set; } = "";
    public string PreviousVersion { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
