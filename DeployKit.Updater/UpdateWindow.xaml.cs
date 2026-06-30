using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using DeployKit.Core.Services;

namespace DeployKit.Updater;

public partial class UpdateWindow : Window
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private CancellationTokenSource? _cts;
    private bool _completed;

    public UpdateWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            var downloadUrl = GetArg(args, "--url");
            var targetDir = GetArg(args, "--target");
            var appName = GetArg(args, "--app");
            var version = GetArg(args, "--version");
            var prev = GetArg(args, "--prev");
            var hostPid = GetArg(args, "--pid");

            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(appName))
            {
                ShowError("Usage: updater --url <download_url> --target <dir> --app <exe> [--version <v>] [--prev <v>] [--pid <pid>]");
                return;
            }

            TitleText.Text = version != null ? $"جاري تحديث v{prev} → v{version}" : "جاري تحديث التطبيق...";
            SubtitleText.Text = appName;

            _cts = new CancellationTokenSource();
            await RunUpdateAsync(downloadUrl, targetDir, appName, prev, hostPid);
        }
        catch (Exception ex)
        {
            LogError($"Window_Loaded: {ex}");
            ShowError($"خطأ غير متوقع: {ex.Message}");
        }
    }

    private static void LogError(string message)
    {
        try
        {
            var logDir = Path.Combine(Path.GetTempPath(), "DeployKit", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, $"updater_{Environment.ProcessId}.log"),
                $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    private async Task RunUpdateAsync(string downloadUrl, string targetDir, string appName, string? prevVersion, string? hostPid)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "DeployKit", "downloads");
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, $"update_{Guid.NewGuid():N}.zip");

            StatusText.Text = "جاري تحميل التحديث...";
            await DownloadFileAsync(downloadUrl, zipPath, _cts!.Token);

            if (_cts.Token.IsCancellationRequested) return;

            // انتظار إغلاق التطبيق الأصلي بالكامل قبل الكتابة على ملفاته
            if (!string.IsNullOrEmpty(hostPid) && int.TryParse(hostPid, out var pid))
            {
                StatusText.Text = "انتظار إغلاق التطبيق...";
                try
                {
                    var hostProcess = Process.GetProcessById(pid);
                    if (!hostProcess.HasExited)
                        hostProcess.WaitForExit(60000);
                }
                catch (ArgumentException) { }
            }

            StatusText.Text = "جاري تطبيق التحديث...";
            ProgressBar.IsIndeterminate = true;
            PercentText.Text = "";

            var backupDir = Path.Combine(Path.GetTempPath(), "DeployKit", "Backups");
            Directory.CreateDirectory(backupDir);

            var applier = new PackageApplier(backupDir);
            var manifest = await applier.LoadManifestAsync(zipPath);

            if (manifest.IsFullPackage)
                await applier.ApplyFullPackageAsync(zipPath, targetDir);
            else
                await applier.ApplyAsync(zipPath, targetDir);

            if (applier.BackupPath != null)
                SaveRollbackInfo(applier.BackupPath, targetDir, prevVersion);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            _completed = true;
            StatusText.Text = "✅ تم التحديث بنجاح!";
            ProgressBar.Value = 100;
            ProgressBar.IsIndeterminate = false;
            CancelBtn.Content = "تشغيل التطبيق";
            CancelBtn.Click -= CancelBtn_Click;
            CancelBtn.Click += RunApp_Click;
            CancelBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 70, 229));

            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                Dispatcher.Invoke(() => RunAppAndClose(targetDir, appName));
            });
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "تم الإلغاء";
        }
        catch (Exception ex)
        {
            ShowError($"فشل التحديث: {ex.Message}");
        }
    }

    private async Task DownloadFileAsync(string url, string destPath, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;

            if (totalBytes > 0)
            {
                var pct = (double)bytesRead / totalBytes * 100;
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Value = pct;
                    PercentText.Text = $"{pct:F0}%";
                    StatusText.Text = $"جاري التحميل... {FormatSize(bytesRead)} / {FormatSize(totalBytes)}";
                });
            }
        }
    }

    private void RunAppAndClose(string targetDir, string appName)
    {
        var appPath = Path.Combine(targetDir, appName);
        if (File.Exists(appPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = targetDir,
                UseShellExecute = true
            });
        }
        Close();
    }

    private void RunApp_Click(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs();
        var targetDir = GetArg(args, "--target") ?? ".";
        var appName = GetArg(args, "--app") ?? "";
        RunAppAndClose(targetDir, appName);
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_completed)
        {
            var result = MessageBox.Show(this,
                "هل تريد إلغاء التحديث؟",
                "تأكيد الإلغاء",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                e.Cancel = true;
        }
    }

    private void ShowError(string message)
    {
        StatusText.Text = "❌ " + message;
        CancelBtn.Content = "إغلاق";
        CancelBtn.Click -= CancelBtn_Click;
        CancelBtn.Click += (_, _) => Close();
        MessageBox.Show(this, message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string GetArg(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        _ => $"{bytes / (1024.0 * 1024):F1} MB"
    };

    private static void SaveRollbackInfo(string backupPath, string targetDir, string? prevVersion)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeployKit", "rollback.json");
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);

            var info = new
            {
                BackupPath = backupPath,
                TargetDir = targetDir,
                PreviousVersion = prevVersion ?? "",
                CreatedAt = DateTime.UtcNow
            };
            File.WriteAllText(path, JsonSerializer.Serialize(info));
        }
        catch { }
    }
}
