using System.IO;
using System.Windows;
using DeployKit.Core.Services;
using Microsoft.Win32;

namespace DeployKit.Gui.ViewModels;

public class ApplyViewModel : BaseViewModel
{
    private readonly PackageApplier _applier;

    private string _packagePath = "";
    public string PackagePath { get => _packagePath; set => SetProperty(ref _packagePath, value); }

    private string _targetDir = "";
    public string TargetDir { get => _targetDir; set => SetProperty(ref _targetDir, value); }

    private string _statusMessage = "اختر ملف التحديث ومجلد التثبيت";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isApplying;
    public bool IsApplying { get => _isApplying; set => SetProperty(ref _isApplying, value); }

    private bool _isComplete;
    public bool IsComplete { get => _isComplete; set => SetProperty(ref _isComplete, value); }

    private bool _hasPackageInfo;
    public bool HasPackageInfo { get => _hasPackageInfo; set => SetProperty(ref _hasPackageInfo, value); }

    private string _packageInfo = "";
    public string PackageInfo { get => _packageInfo; set => SetProperty(ref _packageInfo, value); }

    private double _progress;
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

    private string _backupPath = "";
    public string BackupPath { get => _backupPath; set => SetProperty(ref _backupPath, value); }

    public RelayCommand SelectPackageCommand { get; }
    public RelayCommand SelectTargetCommand { get; }
    public RelayCommandAsync ApplyCommand { get; }

    public ApplyViewModel()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeployKit", "Backups");
        _applier = new PackageApplier(appData);

        SelectPackageCommand = new RelayCommand(_ => SelectPackage());
        SelectTargetCommand = new RelayCommand(_ => SelectTarget());
        ApplyCommand = new RelayCommandAsync(async _ => await ApplyAsync());
    }

    private void SelectPackage()
    {
        var dialog = new OpenFileDialog { Filter = "ZIP files (*.zip)|*.zip" };
        if (dialog.ShowDialog() == true)
        {
            PackagePath = dialog.FileName;
            LoadPackageInfo();
        }
    }

    private async void LoadPackageInfo()
    {
        try
        {
            var manifest = await _applier.LoadManifestAsync(PackagePath);

            var info = $"التطبيق: {manifest.AppName}";
            info += $"\nمن: {manifest.SourceVersion}  ←  إلى: {manifest.TargetVersion}";
            info += $"\n\n📊 الملخص: مضاف: {manifest.Added.Count}  |  معدل: {manifest.Modified.Count}  |  محذوف: {manifest.Deleted.Count}";

            if (manifest.Added.Count > 0)
            {
                info += "\n\n➕ ملفات جديدة:";
                foreach (var f in manifest.Added.Take(5))
                    info += $"\n  • {f.Path}";
                if (manifest.Added.Count > 5)
                    info += $"\n  ... و {manifest.Added.Count - 5} أخرى";
            }

            if (manifest.Modified.Count > 0)
            {
                info += "\n\n📝 ملفات معدلة:";
                foreach (var f in manifest.Modified.Take(5))
                    info += $"\n  • {f.Path}";
                if (manifest.Modified.Count > 5)
                    info += $"\n  ... و {manifest.Modified.Count - 5} أخرى";
            }

            if (manifest.Deleted.Count > 0)
            {
                info += "\n\n🗑️ ملفات محذوفة:";
                foreach (var f in manifest.Deleted)
                    info += $"\n  • {f.Path}";
            }

            PackageInfo = info;
            HasPackageInfo = true;
            StatusMessage = "جاهز للتطبيق. اختر مجلد التثبيت.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في قراءة الحزمة: {ex.Message}";
            HasPackageInfo = false;
        }
    }

    private void SelectTarget()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            TargetDir = dialog.FolderName;
            if (HasPackageInfo)
                StatusMessage = "جاهز - اضغط بدء التحديث";
        }
    }

    private async Task ApplyAsync()
    {
        if (string.IsNullOrWhiteSpace(PackagePath) || string.IsNullOrWhiteSpace(TargetDir))
        {
            StatusMessage = "الرجاء اختيار ملف التحديث ومجلد التثبيت";
            return;
        }

        IsApplying = true;
        IsComplete = false;
        StatusMessage = "جاري تطبيق التحديث...";

        try
        {
            await _applier.ApplyAsync(PackagePath, TargetDir);
            BackupPath = _applier.BackupPath ?? "";
            Progress = 100;
            IsComplete = true;
            StatusMessage = "✅ تم التحديث بنجاح!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ فشل التحديث: {ex.Message}";
        }
        finally
        {
            IsApplying = false;
        }
    }
}
