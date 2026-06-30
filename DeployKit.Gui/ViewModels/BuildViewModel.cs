using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using DeployKit.Core.Services;
using DeployKit.Gui.Helpers;
using Microsoft.Win32;

namespace DeployKit.Gui.ViewModels;

public class BuildViewModel : BaseViewModel
{
    private readonly PackageBuilder _builder = new();
    private readonly Action<ReleaseRecord>? _onReleaseCreated;

    private string _newDir = "";
    public string NewDir { get => _newDir; set { SetProperty(ref _newDir, value); UpdateCanBuild(); } }

    private string _appName = "";
    public string AppName { get => _appName; set => SetProperty(ref _appName, value); }

    private string _version = "";
    public string Version { get => _version; set { SetProperty(ref _version, value); UpdateCanBuild(); } }

    private string _releaseNotes = "";
    public string ReleaseNotes { get => _releaseNotes; set => SetProperty(ref _releaseNotes, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }

    private double _progress;
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

    private bool _isBuilding;
    public bool IsBuilding { get => _isBuilding; set => SetProperty(ref _isBuilding, value); }

    private bool _isMandatory;
    public bool IsMandatory { get => _isMandatory; set => SetProperty(ref _isMandatory, value); }

    private bool _isWorking;
    public bool IsWorking { get => _isWorking; set => SetProperty(ref _isWorking, value); }

    private bool _hasApiKey;
    public bool HasApiKey { get => _hasApiKey; set => SetProperty(ref _hasApiKey, value); }

    private bool _canBuild;
    public bool CanBuild { get => _canBuild; set => SetProperty(ref _canBuild, value); }

    public RelayCommand SelectNewDirCommand { get; }
    public RelayCommandAsync BuildPackageCommand { get; }
    public RelayCommandAsync UploadToCloudCommand { get; }
    public RelayCommandAsync CreateGitHubReleaseCommand { get; }

    public BuildViewModel(Action<ReleaseRecord>? onReleaseCreated = null)
    {
        _onReleaseCreated = onReleaseCreated;
        SelectNewDirCommand = new RelayCommand(_ => SelectNewDir());
        BuildPackageCommand = new RelayCommandAsync(async _ => await BuildPackageAsync());
        UploadToCloudCommand = new RelayCommandAsync(async _ => await UploadToCloudAsync());
        CreateGitHubReleaseCommand = new RelayCommandAsync(async _ => await CreateGitHubReleaseAsync());
        CheckApiKey();
    }

    private void UpdateCanBuild()
    {
        CanBuild = !string.IsNullOrWhiteSpace(NewDir) && !string.IsNullOrWhiteSpace(Version);
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
                var data = JsonSerializer.Deserialize<CloudSettings>(json);
                HasApiKey = data != null && !string.IsNullOrEmpty(data.ApiKey);
            }
        }
        catch { HasApiKey = false; }
    }

    private void SelectNewDir()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            NewDir = dialog.FolderName;
            AutoDetect();
        }
    }

    private void AutoDetect()
    {
        if (string.IsNullOrWhiteSpace(AppName))
            AppName = Path.GetFileName(NewDir);

        if (string.IsNullOrWhiteSpace(Version))
        {
            try
            {
                var exeFiles = Directory.GetFiles(NewDir, "*.exe", SearchOption.TopDirectoryOnly);
                foreach (var exe in exeFiles)
                {
                    var ver = FileVersionInfo.GetVersionInfo(exe);
                    if (ver.FileVersion != null)
                    {
                        var parts = ver.FileVersion.Split('.');
                        if (parts.Length >= 2)
                        {
                            Version = $"{parts[0]}.{parts[1]}.{parts[2]}";
                            break;
                        }
                    }
                }
            }
            catch { }
        }
    }

    private async Task BuildPackageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDir))
        {
            StatusMessage = "الرجاء اختيار مجلد التطبيق الجديد";
            return;
        }
        if (string.IsNullOrWhiteSpace(Version))
        {
            StatusMessage = "الرجاء إدخال رقم الإصدار";
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Filter = "ZIP files (*.zip)|*.zip",
            FileName = $"{AppName}_v{Version}.zip"
        };

        if (saveDialog.ShowDialog() != true) return;

        IsBuilding = true;
        StatusMessage = "جاري بناء حزمة التحديث...";
        Progress = 0;

        try
        {
            OutputPath = await _builder.BuildFullPackageAsync(NewDir, AppName, Version, saveDialog.FileName);
            Progress = 100;

            var fileSize = new FileInfo(OutputPath).Length;

            var record = new ReleaseRecord
            {
                AppName = AppName,
                FromVersion = "",
                ToVersion = Version,
                ReleaseNotes = ReleaseNotes,
                ZipPath = OutputPath,
                FileSize = fileSize,
                CreatedAt = DateTime.Now
            };

            _onReleaseCreated?.Invoke(record);
            StatusMessage = $"✅ تم إنشاء الحزمة: {OutputPath} ({FormatSize(fileSize)})";
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

    private CloudSettings? LoadCloudSettings()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeployKit", "settings.json");
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CloudSettings>(json);
        }
        catch { return null; }
    }

    private async Task UploadToCloudAsync()
    {
        if (string.IsNullOrEmpty(OutputPath) || !File.Exists(OutputPath)) return;

        var data = LoadCloudSettings();
        if (data == null || string.IsNullOrEmpty(data.ApiKey))
        {
            StatusMessage = "⚠️ لا يوجد API Key. سجل تطبيقك من صفحة الإعدادات أولاً";
            return;
        }

        IsWorking = true;
        StatusMessage = "☁️ جاري التسجيل في السحابة...";

        try
        {
            var cloud = new CloudService(data.CloudUrl);
            var downloadUrl = data.GitHubRawUrl;
            if (string.IsNullOrWhiteSpace(downloadUrl))
                downloadUrl = $"https://github.com/{data.GitHubUser}/{data.GitHubRepo}/releases/download/v{Version}/update.zip";

            var result = await cloud.UploadAsync(data.ApiKey, Version, downloadUrl, ReleaseNotes, IsMandatory);
            StatusMessage = $"☁️✅ تم التسجيل! الإصدار {result.Version} في السحابة";
        }
        catch (Exception ex)
        {
            StatusMessage = $"☁️❌ فشل التسجيل السحابي: {ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }

    private async Task CreateGitHubReleaseAsync()
    {
        if (string.IsNullOrEmpty(OutputPath) || !File.Exists(OutputPath)) return;

        var data = LoadCloudSettings();
        if (data == null || string.IsNullOrWhiteSpace(data.GitHubToken))
        {
            StatusMessage = "⚠️ لا يوجد GitHub Token. أضفه من صفحة الإعدادات أولاً";
            return;
        }

        IsWorking = true;
        StatusMessage = "🐙 جاري إنشاء الإصدار على GitHub...";

        try
        {
            var tag = $"v{Version}";
            var repo = $"{data.GitHubUser}/{data.GitHubRepo}";
            var zipName = $"{AppName}_v{Version}.zip";

            var createUrl = $"https://api.github.com/repos/{repo}/releases";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DeployKit");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GitHubToken);

            var createBody = JsonSerializer.Serialize(new { tag_name = tag, name = tag, body = ReleaseNotes, prerelease = false });
            var createResponse = await client.PostAsync(createUrl, new StringContent(createBody, Encoding.UTF8, "application/json"));
            createResponse.EnsureSuccessStatusCode();
            var createResult = JsonSerializer.Deserialize<JsonElement>(await createResponse.Content.ReadAsStringAsync());
            var uploadUrl = createResult.GetProperty("upload_url").GetString()!.Replace("{?name,label}", $"?name={zipName}");

            await using var fileStream = File.OpenRead(OutputPath);
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            var uploadResponse = await client.PostAsync(uploadUrl, fileContent);
            uploadResponse.EnsureSuccessStatusCode();

            var releaseUrl = createResult.GetProperty("html_url").GetString()!;
            StatusMessage = $"🐙✅ تم الرفع! {releaseUrl}";

            data.GitHubRawUrl = $"https://github.com/{repo}/releases/download/{tag}/{zipName}";
            var savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeployKit", "settings.json");
            File.WriteAllText(savePath, JsonSerializer.Serialize(data));
        }
        catch (Exception ex)
        {
            StatusMessage = $"🐙❌ فشل إنشاء الإصدار على GitHub: {ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024):F1} MB"
    };

    internal class CloudSettings
    {
        public string? ApiKey { get; set; }
        public string? CloudUrl { get; set; }
        public string? GitHubUser { get; set; }
        public string? GitHubRepo { get; set; }
        public string? GitHubToken { get; set; }
        public string? GitHubRawUrl { get; set; }
    }
}
