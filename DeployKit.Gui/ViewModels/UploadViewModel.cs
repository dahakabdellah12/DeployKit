using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using DeployKit.Gui.Helpers;
using Microsoft.Win32;

namespace DeployKit.Gui.ViewModels;

public class UploadViewModel : BaseViewModel
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromMinutes(5) };
    private string _filePath = "";
    public string FilePath { get => _filePath; set { SetProperty(ref _filePath, value); UpdateCanUpload(); } }

    private string _appName = "";
    public string AppName { get => _appName; set => SetProperty(ref _appName, value); }

    private string _version = "";
    public string Version { get => _version; set { SetProperty(ref _version, value); UpdateCanUpload(); } }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private string _downloadUrl = "";
    public string DownloadUrl { get => _downloadUrl; set { SetProperty(ref _downloadUrl, value); HasDownloadUrl = !string.IsNullOrEmpty(value); } }

    private bool _hasDownloadUrl;
    public bool HasDownloadUrl { get => _hasDownloadUrl; set => SetProperty(ref _hasDownloadUrl, value); }

    private double _progress;
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

    public bool HasProgress => IsWorking;

    private bool _isWorking;
    public bool IsWorking { get => _isWorking; set { SetProperty(ref _isWorking, value); OnPropertyChanged(nameof(HasProgress)); } }

    private bool _canUpload;
    public bool CanUpload { get => _canUpload; set => SetProperty(ref _canUpload, value); }

    public RelayCommand SelectFileCommand { get; }
    public RelayCommandAsync UploadCommand { get; }
    public RelayCommand CopyDownloadUrlCommand { get; }

    public UploadViewModel()
    {
        SelectFileCommand = new RelayCommand(_ => SelectFile());
        UploadCommand = new RelayCommandAsync(async _ => await UploadAsync());
        CopyDownloadUrlCommand = new RelayCommand(_ => CopyUrl());
    }

    private void UpdateCanUpload()
    {
        CanUpload = !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath)
                    && !string.IsNullOrWhiteSpace(Version);
    }

    private void SelectFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "ZIP files (*.zip)|*.zip",
            Title = "اختر ملف حزمة التحديث"
        };
        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
            AutoDetect();
        }
    }

    private void AutoDetect()
    {
        var name = Path.GetFileNameWithoutExtension(FilePath);

        if (string.IsNullOrWhiteSpace(AppName))
        {
            var parts = name.Split('_', 'v');
            if (parts.Length >= 2)
                AppName = parts[0];
            else
                AppName = name;
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            var match = Regex.Match(name, @"v?(\d+\.\d+\.\d+)");
            if (match.Success)
                Version = match.Groups[1].Value;
        }
    }

    private async Task UploadAsync()
    {
        DownloadUrl = "";
        HasDownloadUrl = false;

        var cloudVer = await CheckCloudVersionAsync();
        if (!string.IsNullOrEmpty(cloudVer))
        {
            var current = TryParseVersion(cloudVer);
            var newVer = TryParseVersion(Version);
            if (current != null && newVer != null && newVer <= current)
            {
                var result = MessageBox.Show(
                    $"⚠️ الإصدار في السحابة ({cloudVer}) أحدث أو يساوي الإصدار الجديد ({Version}).\nهل تريد المتابعة؟",
                    "تحذير الإصدار", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    StatusMessage = "تم إلغاء الرفع";
                    return;
                }
            }
        }

        await UploadToGitHubAsync();

        if (!string.IsNullOrEmpty(DownloadUrl))
            await UploadToCloudAsync();
    }

    private async Task<string> CheckCloudVersionAsync()
    {
        var data = LoadSettings();
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

    private async Task UploadToGitHubAsync()
    {
        var data = LoadSettings();
        if (data == null || string.IsNullOrWhiteSpace(data.GitHubToken))
        {
            StatusMessage = "⚠️ لا يوجد GitHub Token. أضفه من صفحة الإعدادات أولاً";
            return;
        }
        if (string.IsNullOrWhiteSpace(data.GitHubUser) || string.IsNullOrWhiteSpace(data.GitHubRepo))
        {
            StatusMessage = "⚠️ GitHub User/Repo غير مضبوطين في الإعدادات";
            return;
        }

        IsWorking = true;
        StatusMessage = "🐙 جاري رفع الحزمة إلى GitHub Releases...";

        try
        {
            var tag = $"v{Version}";
            var repo = $"{data.GitHubUser}/{data.GitHubRepo}";
            var zipName = Path.GetFileName(FilePath);

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
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

            await using var fileStream = File.OpenRead(FilePath);
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
            StatusMessage = $"🐙✅ تم الرفع! {releaseUrl}";

            data.GitHubRawUrl = downloadUrl;
            var savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeployKit", "settings.json");
            data.GitHubTokenEncrypted = SecureStorage.Encrypt(data.GitHubToken);
            data.GitHubToken = null;
            data.ApiKeyEncrypted = SecureStorage.Encrypt(data.ApiKey);
            data.ApiKey = null;
            data.AdminTokenEncrypted = SecureStorage.Encrypt(data.AdminToken);
            data.AdminToken = null;
            File.WriteAllText(savePath, JsonSerializer.Serialize(data));
        }
        catch (Exception ex)
        {
            StatusMessage = $"🐙❌ فشل الرفع: {ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }

    private async Task UploadToCloudAsync()
    {
        var data = LoadSettings();
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
            // Cloud registration is optional
        }
        finally
        {
            IsWorking = false;
        }
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

    private void CopyUrl()
    {
        if (!string.IsNullOrEmpty(DownloadUrl))
        {
            Clipboard.SetText(DownloadUrl);
            StatusMessage = "📋 تم نسخ رابط التحميل";
        }
    }

    private static CloudSettings? LoadSettings()
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

    private class CloudSettings
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
}
