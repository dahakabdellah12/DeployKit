using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using DeployKit.Core.Models;
using DeployKit.Core.Services;
using DeployKit.Gui.Helpers;
using Microsoft.Win32;

namespace DeployKit.Gui.ViewModels;

public class BuildViewModel : BaseViewModel
{
    private readonly FileComparer _comparer = new();
    private readonly PackageBuilder _builder = new();
    private readonly Action<string, string, string, string, string, string>? _onPackageBuilt;

    private string _oldDir = "";
    public string OldDir { get => _oldDir; set => SetProperty(ref _oldDir, value); }

    private string _newDir = "";
    public string NewDir { get => _newDir; set => SetProperty(ref _newDir, value); }

    private string _appName = "";
    public string AppName { get => _appName; set => SetProperty(ref _appName, value); }

    private string _sourceVersion = "";
    public string SourceVersion { get => _sourceVersion; set => SetProperty(ref _sourceVersion, value); }

    private string _targetVersion = "";
    public string TargetVersion { get => _targetVersion; set => SetProperty(ref _targetVersion, value); }

    private bool _isComparing;
    public bool IsComparing { get => _isComparing; set => SetProperty(ref _isComparing, value); }

    private bool _hasResult;
    public bool HasResult { get => _hasResult; set => SetProperty(ref _hasResult, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }

    private double _progress;
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

    private bool _isBuilding;
    public bool IsBuilding { get => _isBuilding; set => SetProperty(ref _isBuilding, value); }

    private string _savingsInfo = "";
    public string SavingsInfo { get => _savingsInfo; set => SetProperty(ref _savingsInfo, value); }

    private bool _isUploading;
    public bool IsUploading { get => _isUploading; set => SetProperty(ref _isUploading, value); }

    private bool _hasApiKey;
    public bool HasApiKey { get => _hasApiKey; set => SetProperty(ref _hasApiKey, value); }

    public ObservableCollection<ChangeItem> Changes { get; } = [];

    public RelayCommand SelectOldDirCommand { get; }
    public RelayCommand SelectNewDirCommand { get; }
    public RelayCommandAsync CompareCommand { get; }
    public RelayCommandAsync BuildPackageCommand { get; }
    public RelayCommandAsync UploadToCloudCommand { get; }

    public BuildViewModel(Action<string, string, string, string, string, string>? onPackageBuilt = null)
    {
        _onPackageBuilt = onPackageBuilt;
        SelectOldDirCommand = new RelayCommand(_ => SelectFolder(nameof(OldDir)));
        SelectNewDirCommand = new RelayCommand(_ => SelectFolder(nameof(NewDir)));
        CompareCommand = new RelayCommandAsync(async _ => await CompareAsync());
        BuildPackageCommand = new RelayCommandAsync(async _ => await BuildPackageAsync());
        UploadToCloudCommand = new RelayCommandAsync(async _ => await UploadToCloudAsync());
        CheckApiKey();
    }

    private void CheckApiKey()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeployKit", "settings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                HasApiKey = data != null && !string.IsNullOrEmpty(data.ApiKey);
            }
        }
        catch { HasApiKey = false; }
    }

    private void SelectFolder(string property)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            if (property == nameof(OldDir)) OldDir = dialog.FolderName;
            else NewDir = dialog.FolderName;
        }
    }

    private async Task CompareAsync()
    {
        if (string.IsNullOrWhiteSpace(OldDir) || string.IsNullOrWhiteSpace(NewDir))
        {
            StatusMessage = "الرجاء اختيار المجلدين";
            return;
        }

        IsComparing = true;
        HasResult = false;
        StatusMessage = "جاري المقارنة...";
        Changes.Clear();

        try
        {
            var result = await _comparer.CompareAsync(OldDir, NewDir);

            foreach (var f in result.Added)
                Changes.Add(new ChangeItem { Path = f.RelativePath, Type = "➕ جديد", Size = FormatSize(f.NewSize), Color = "#4CAF50" });
            foreach (var f in result.Modified)
                Changes.Add(new ChangeItem { Path = f.RelativePath, Type = "📝 معدل", Size = FormatSize(f.NewSize), Color = "#FF9800" });
            foreach (var f in result.Deleted)
                Changes.Add(new ChangeItem { Path = f.RelativePath, Type = "🗑️ حذف", Size = "", Color = "#F44336" });

            AppName = Path.GetFileName(OldDir);
            SourceVersion = "1.0.0";
            TargetVersion = "1.1.0";

            var fullSize = result.FullPackageSize;
            var patchSize = result.PackageSize;
            var savings = fullSize > 0 ? (1.0 - (double)patchSize / fullSize) * 100 : 0;

            if (savings > 0)
                SavingsInfo = $"💾 التوفير بالباتشات: {FormatSize(fullSize)} → {FormatSize(patchSize)} (توفير {savings:F1}%)";
            else
                SavingsInfo = "";

            StatusMessage = $"تم العثور على {result.TotalChanged} تغييرات | حجم الحزمة: {FormatSize(result.PackageSize)}";
            HasResult = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
        }
        finally
        {
            IsComparing = false;
        }
    }

    private async Task BuildPackageAsync()
    {
        if (!HasResult) return;

        var saveDialog = new SaveFileDialog
        {
            Filter = "ZIP files (*.zip)|*.zip",
            FileName = $"{AppName}_{SourceVersion}_to_{TargetVersion}.zip"
        };

        if (saveDialog.ShowDialog() != true) return;

        IsBuilding = true;
        StatusMessage = "جاري بناء حزمة التحديث بالباتشات الثنائية...";
        Progress = 0;

        try
        {
            var result = await _comparer.CompareAsync(OldDir, NewDir);
            OutputPath = await _builder.BuildAsync(OldDir, NewDir, result, AppName, SourceVersion, TargetVersion, saveDialog.FileName);
            Progress = 100;

            var filesize = new FileInfo(OutputPath).Length;
            var fullSize = result.FullPackageSize;
            var patchSize = result.PackageSize;
            var totalSavings = fullSize > 0 ? (1.0 - (double)patchSize / fullSize) * 100 : 0;

            StatusMessage = $"✅ تم إنشاء الحزمة: {OutputPath} ({FormatSize(filesize)})";
            if (totalSavings > 0)
                StatusMessage += $" | توفير: {totalSavings:F1}%";

            _onPackageBuilt?.Invoke(AppName, SourceVersion, TargetVersion, OldDir, NewDir, OutputPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ خطأ: {ex.Message}";
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private async Task UploadToCloudAsync()
    {
        if (!HasResult || string.IsNullOrEmpty(OutputPath)) return;

        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeployKit", "settings.json");
            if (!File.Exists(path))
            {
                StatusMessage = "⚠️ لا يوجد API Key. سجل تطبيقك من صفحة الإعدادات أولاً";
                return;
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data == null || string.IsNullOrEmpty(data.ApiKey))
            {
                StatusMessage = "⚠️ لا يوجد API Key. سجل تطبيقك من صفحة الإعدادات أولاً";
                return;
            }

            IsUploading = true;
            StatusMessage = "☁️ جاري التسجيل في السحابة...";

            var cloud = new CloudService(data.CloudUrl);
            var downloadUrl = $"https://github.com/{Uri.EscapeDataString(AppName)}/releases/download/v{TargetVersion}/update.zip";
            var result = await cloud.UploadAsync(data.ApiKey, TargetVersion, downloadUrl,
                $"تحديث {AppName} من {SourceVersion} إلى {TargetVersion}");

            StatusMessage = $"☁️✅ تم التسجيل! الإصدار {result.Version} — الرابط: {result.DownloadUrl}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"☁️❌ فشل الرفع: {ex.Message}";
        }
        finally
        {
            IsUploading = false;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024):F1} MB"
    };

    private class SettingsData
    {
        public string? ApiKey { get; set; }
        public string? CloudUrl { get; set; }
    }
}

public class ChangeItem
{
    public string Path { get; set; } = "";
    public string Type { get; set; } = "";
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
}
