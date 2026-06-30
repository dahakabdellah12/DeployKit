using System.Diagnostics;
using System.IO;
using System.Windows;
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

        VersionText.Text = _result.IsFullPackage
            ? $"v{config.CurrentVersion}  →  v{result.LatestVersion}  (📦 كامل)"
            : $"v{config.CurrentVersion}  →  v{result.LatestVersion}";

        NotesText.Text = string.IsNullOrWhiteSpace(result.ReleaseNotes)
            ? "لا توجد ملاحظات إصدار."
            : result.ReleaseNotes;

        if (result.IsMandatory)
            LaterBtn.IsEnabled = false;

        var rollbackInfo = DeployKit.GetRollbackInfo();
        if (rollbackInfo != null)
        {
            RollbackBtn.Visibility = Visibility.Visible;
            RollbackBtn.Content = $"↩️  رجوع إلى v{rollbackInfo.PreviousVersion}";
        }
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeployKit.Updater.exe");
        if (!File.Exists(updaterPath))
        {
            MessageBox.Show(this,
                $"لم يتم العثور على مشغل التحديث: {updaterPath}",
                "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var targetDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        var exeName = Path.GetFileName(Environment.ProcessPath!);
        var args = $"--url \"{_result.DownloadUrl}\" --target \"{targetDir}\" --app \"{exeName}\" --version {_result.LatestVersion} --prev {_config.CurrentVersion} --pid {Environment.ProcessId}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = args,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(updaterPath)
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"فشل تشغيل مشغل التحديث: {ex.Message}",
                "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Application.Current.Shutdown();
    }

    private void OnLaterClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void OnRollbackClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this,
                "هل تريد الرجوع للإصدار السابق؟",
                "رجوع",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        RollbackBtn.IsEnabled = false;

        var error = await DeployKit.RollbackAsync();
        if (!string.IsNullOrEmpty(error))
        {
            MessageBox.Show(this, error, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            RollbackBtn.IsEnabled = true;
        }
        else
        {
            MessageBox.Show(this,
                "تم الرجوع للإصدار السابق بنجاح.",
                "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
    }
}
