using System.Diagnostics;
using System.IO;
using System.Windows;
using DeployKit.Core.Services;
using DeployKit.Integration.Models;

namespace DeployKit.Integration.UI;

public partial class UpdateWindow : Window
{
    private readonly UpdateResult _result;
    private readonly UpdateConfig _config;

    public UpdateWindow(UpdateResult result, UpdateConfig config)
    {
        InitializeComponent();
        _result = result;
        _config = config;

        VersionText.Text = $"v{config.CurrentVersion}  →  v{result.LatestVersion}";
        NotesText.Text = string.IsNullOrWhiteSpace(result.ReleaseNotes)
            ? "لا توجد ملاحظات إصدار."
            : result.ReleaseNotes;

        if (result.IsMandatory)
            LaterBtn.IsEnabled = false;
    }

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        LaterBtn.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "DeployKit", "downloads");
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, $"update_{Guid.NewGuid():N}.zip");

            var client = new Services.UpdateClient();
            DownloadProgress.Value = 0;

            await client.DownloadPackageAsync(_result.DownloadUrl, zipPath,
                new Progress<double>(p => Dispatcher.Invoke(() => DownloadProgress.Value = p)));

            ProgressLabel.Text = "جاري تطبيق التحديث...";

            var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeployKit.Updater.exe");
            var targetDir = AppDomain.CurrentDomain.BaseDirectory;
            var exeName = Path.GetFileName(Environment.ProcessPath!);
            var pid = Environment.ProcessId;

            var args = $"--zip \"{zipPath}\" --target \"{targetDir}\" --app \"{exeName}\" --pid {pid}";

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = args,
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"فشل التحديث: {ex.Message}",
                "خطأ",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            UpdateBtn.IsEnabled = true;
            LaterBtn.IsEnabled = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnLaterClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
