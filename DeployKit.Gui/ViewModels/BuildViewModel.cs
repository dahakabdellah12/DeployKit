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
    public string AppName { get => _appName; set { SetProperty(ref _appName, value); UpdateCanBuild(); } }
    private string _version = "";
    public string Version { get => _version; set { SetProperty(ref _version, value); UpdateCanBuild(); } }
    private string _fromVersion = "";
    private string _releaseNotes = "";

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }

    private string _downloadUrl = "";
    public string DownloadUrl { get => _downloadUrl; set => SetProperty(ref _downloadUrl, value); }

    private double _progress;
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

    public bool HasProgress => IsBuilding || IsWorking;

    private bool _isBuilding;
    public bool IsBuilding { get => _isBuilding; set { SetProperty(ref _isBuilding, value); OnPropertyChanged(nameof(HasProgress)); } }

    private bool _isWorking;
    public bool IsWorking { get => _isWorking; set { SetProperty(ref _isWorking, value); OnPropertyChanged(nameof(HasProgress)); } }

    private bool _hasDownloadUrl;
    public bool HasDownloadUrl { get => _hasDownloadUrl; set => SetProperty(ref _hasDownloadUrl, value); }

    private bool _canBuild;
    public bool CanBuild { get => _canBuild; set => SetProperty(ref _canBuild, value); }

    public RelayCommand SelectNewDirCommand { get; }
    public RelayCommandAsync BuildPackageCommand { get; }
    public RelayCommand CopyDownloadUrlCommand { get; }

    public BuildViewModel(Action<ReleaseRecord>? onReleaseCreated = null)
    {
        _onReleaseCreated = onReleaseCreated;
        SelectNewDirCommand = new RelayCommand(_ => SelectNewDir());
        BuildPackageCommand = new RelayCommandAsync(async _ => await BuildPackageAsync());
        CopyDownloadUrlCommand = new RelayCommand(_ => CopyDownloadUrl());
    }

    private void UpdateCanBuild()
    {
        CanBuild = !string.IsNullOrWhiteSpace(NewDir) && !string.IsNullOrWhiteSpace(AppName) && !string.IsNullOrWhiteSpace(Version);
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

        AutoDetect();

        if (string.IsNullOrWhiteSpace(Version))
        {
            StatusMessage = "تعذر اكتشاف رقم الإصدار. تأكد من وجود ملف .exe في المجلد";
            return;
        }

        _fromVersion = await CheckCloudVersionAsync();

        if (!string.IsNullOrEmpty(_fromVersion))
        {
            var cloudVer = TryParseVersion(_fromVersion);
            var newVer = TryParseVersion(Version);
            if (cloudVer != null && newVer != null && newVer <= cloudVer)
            {
                var result = MessageBox.Show(
                    $"⚠️ الإصدار في السحابة ({_fromVersion}) أحدث أو يساوي الإصدار الجديد ({Version}).\nهل تريد المتابعة؟",
                    "تحذير الإصدار", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    StatusMessage = "تم إلغاء البناء — الإصدار الجديد ليس أحدث من إصدار السحابة";
                    return;
                }
            }
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
        DownloadUrl = "";
        HasDownloadUrl = false;

        try
        {
            OutputPath = await _builder.BuildFullPackageAsync(NewDir, AppName, Version, saveDialog.FileName);
            Progress = 100;

            var fileSize = new FileInfo(OutputPath).Length;

            var record = new ReleaseRecord
            {
                AppName = AppName,
                FromVersion = _fromVersion,
                ToVersion = Version,
                ReleaseNotes = _releaseNotes,
                ZipPath = OutputPath,
                FileSize = fileSize,
                CreatedAt = DateTime.Now
            };

            StatusMessage = $"✅ تم إنشاء الحزمة ({FormatSize(fileSize)})";

            await UploadToGitHubReleaseAsync(record);

            await UploadToCloudAsync();

            _onReleaseCreated?.Invoke(record);
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

    private async Task UploadToGitHubReleaseAsync(ReleaseRecord record)
    {
        var data = LoadCloudSettings();
        if (data == null || string.IsNullOrWhiteSpace(data.GitHubToken))
        {
            StatusMessage = "✅ تم إنشاء الحزمة ⚠️ لكن لا يوجد GitHub Token. أضفه من الإعدادات.";
            return;
        }
        if (string.IsNullOrWhiteSpace(data.GitHubUser) || string.IsNullOrWhiteSpace(data.GitHubRepo))
        {
            StatusMessage = "✅ تم إنشاء الحزمة ⚠️ لكن GitHub User/Repo غير مضبوطين.";
            return;
        }

        IsWorking = true;
        StatusMessage = "🐙 جاري رفع الحزمة إلى GitHub Releases...";

        try
        {
            var tag = $"v{Version}";
            var repo = $"{data.GitHubUser}/{data.GitHubRepo}";
            var zipName = $"{AppName}_v{Version}.zip";

            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(System.Threading.Timeout.Infinite) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DeployKit");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GitHubToken);

            await DeleteExistingReleaseAsync(client, repo, tag);

            var createUrl = $"https://api.github.com/repos/{repo}/releases";
            var createBody = JsonSerializer.Serialize(new
            {
                tag_name = tag,
                name = tag,
                body = "",
                prerelease = false
            });
            var createResponse = await client.PostAsync(createUrl,
                new StringContent(createBody, Encoding.UTF8, "application/json"));
            if (!createResponse.IsSuccessStatusCode)
            {
                var errBody = await createResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException($"GitHub create release failed ({createResponse.StatusCode}): {errBody}");
            }
            var createResult = JsonSerializer.Deserialize<JsonElement>(
                await createResponse.Content.ReadAsStringAsync());

            var uploadUrl = createResult.GetProperty("upload_url").GetString()!
                .Replace("{?name,label}", $"?name={zipName}");
            var releaseUrl = createResult.GetProperty("html_url").GetString()!;

            await using var fileStream = File.OpenRead(OutputPath);
            var progressStream = new ProgressStream(fileStream, pct =>
                Application.Current.Dispatcher.Invoke(() => Progress = pct));
            using var fileContent = new StreamContent(progressStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            fileContent.Headers.ContentLength = fileStream.Length;
            var uploadResponse = await client.PostAsync(uploadUrl, fileContent);
            if (!uploadResponse.IsSuccessStatusCode)
            {
                var errBody = await uploadResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException($"GitHub upload file failed ({uploadResponse.StatusCode}): {errBody}");
            }

            var assetResult = JsonSerializer.Deserialize<JsonElement>(
                await uploadResponse.Content.ReadAsStringAsync());
            var downloadUrl = assetResult.GetProperty("browser_download_url").GetString()!;

            DownloadUrl = downloadUrl;
            HasDownloadUrl = true;
            record.GitHubReleaseUrl = releaseUrl;
            record.CreatedGitHubRelease = true;

            data.GitHubRawUrl = downloadUrl;
            var savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeployKit", "settings.json");
            File.WriteAllText(savePath, JsonSerializer.Serialize(data));

            StatusMessage = $"🐙✅ تم الرفع! الرابط: {downloadUrl}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"✅ تم إنشاء الحزمة ❌ فشل رفع GitHub: {ex.Message}";
            DownloadUrl = "";
            HasDownloadUrl = false;
        }
        finally
        {
            IsWorking = false;
        }
    }

    private async Task UploadToCloudAsync()
    {
        if (string.IsNullOrEmpty(OutputPath) || !File.Exists(OutputPath)) return;

        var data = LoadCloudSettings();
        if (data == null || string.IsNullOrEmpty(data.ApiKey))
            return;

        IsWorking = true;
        StatusMessage = "☁️ جاري التسجيل في السحابة...";

        try
        {
            var cloud = new CloudService(data.CloudUrl, data.AdminToken);
            var downloadUrl = data.GitHubRawUrl;
            if (string.IsNullOrWhiteSpace(downloadUrl))
                downloadUrl = $"https://github.com/{data.GitHubUser}/{data.GitHubRepo}/releases/download/v{Version}/update.zip";

            await cloud.UploadAsync(data.ApiKey, Version, downloadUrl, "", false);
            StatusMessage = "☁️✅ تم التسجيل في السحابة";
        }
        catch
        {
            StatusMessage = "✅ تم رفع الحزمة لكن تعذر التسجيل في السحابة";
        }
        finally
        {
            IsWorking = false;
        }
    }

    private async Task<string> CheckCloudVersionAsync()
    {
        var data = LoadCloudSettings();
        if (data == null || string.IsNullOrEmpty(data.ApiKey))
            return "";

        try
        {
            var cloud = new CloudService(data.CloudUrl, data.AdminToken);
            var result = await cloud.CheckVersionAsync(data.ApiKey);
            if (result != null && !string.IsNullOrEmpty(result.Version))
            {
                StatusMessage = $"☁️ آخر إصدار في السحابة: {result.Version}";
                return result.Version;
            }
        }
        catch { }

        return "";
    }

    private static Version? TryParseVersion(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var parts = v.Trim().Split('.');
        if (parts.Length < 2) return null;
        if (!int.TryParse(parts[0], out var major)) return null;
        if (!int.TryParse(parts[1], out var minor)) return null;
        var build = parts.Length > 2 && int.TryParse(parts[2], out var b) ? b : 0;
        return new Version(major, minor, Math.Max(build, 0));
    }

    private static async Task DeleteExistingReleaseAsync(HttpClient client, string repo, string tag)
    {
        var getUrl = $"https://api.github.com/repos/{repo}/releases/tags/{tag}";
        var getResponse = await client.GetAsync(getUrl);
        if (!getResponse.IsSuccessStatusCode) return;

        var existing = JsonSerializer.Deserialize<JsonElement>(
            await getResponse.Content.ReadAsStringAsync());
        var releaseId = existing.GetProperty("id").GetInt32();

        await client.DeleteAsync($"https://api.github.com/repos/{repo}/releases/{releaseId}");

        await client.DeleteAsync($"https://api.github.com/repos/{repo}/git/refs/tags/{tag}");
    }

    private void CopyDownloadUrl()
    {
        if (!string.IsNullOrEmpty(DownloadUrl))
        {
            Clipboard.SetText(DownloadUrl);
            StatusMessage = "📋 تم نسخ رابط التحميل";
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
        public string? GitHubTokenEncrypted { get; set; }
        public string? GitHubRawUrl { get; set; }
        public string? AdminToken { get; set; }
        public string? ApiKeyEncrypted { get; set; }
        public string? AdminTokenEncrypted { get; set; }
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
            var data = JsonSerializer.Deserialize<CloudSettings>(json);
            if (data != null)
            {
                if (!string.IsNullOrEmpty(data.GitHubTokenEncrypted))
                    data.GitHubToken = SecureStorage.Decrypt(data.GitHubTokenEncrypted);
                if (!string.IsNullOrEmpty(data.ApiKeyEncrypted))
                    data.ApiKey = SecureStorage.Decrypt(data.ApiKeyEncrypted);
                if (!string.IsNullOrEmpty(data.AdminTokenEncrypted))
                    data.AdminToken = SecureStorage.Decrypt(data.AdminTokenEncrypted);
            }
            return data;
        }
        catch { return null; }
    }
}
