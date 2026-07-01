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
    private string _filePath = "";
    public string FilePath { get => _filePath; set { SetProperty(ref _filePath, value); UpdateCanUpload(); } }

    private string _appName = "";
    public string AppName { get => _appName; set => SetProperty(ref _appName, value); }

    private string _version = "";
    public string Version { get => _version; set { SetProperty(ref _version, value); UpdateCanUpload(); } }

    private string _releaseNotes = "";
    public string ReleaseNotes { get => _releaseNotes; set => SetProperty(ref _releaseNotes, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private string _downloadUrl = "";
    public string DownloadUrl { get => _downloadUrl; set { SetProperty(ref _downloadUrl, value); HasDownloadUrl = !string.IsNullOrEmpty(value); } }

    private bool _hasDownloadUrl;
    public bool HasDownloadUrl { get => _hasDownloadUrl; set => SetProperty(ref _hasDownloadUrl, value); }

    private bool _isWorking;
    public bool IsWorking { get => _isWorking; set => SetProperty(ref _isWorking, value); }

    private bool _canUpload;
    public bool CanUpload { get => _canUpload; set => SetProperty(ref _canUpload, value); }

    private bool _uploadToGitHub = true;
    public bool UploadToGitHub { get => _uploadToGitHub; set => SetProperty(ref _uploadToGitHub, value); }

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
            var match = System.Text.RegularExpressions.Regex.Match(name, @"v?(\d+\.\d+\.\d+)");
            if (match.Success)
                Version = match.Groups[1].Value;
        }
    }

    private async Task UploadAsync()
    {
        DownloadUrl = "";
        HasDownloadUrl = false;

        if (UploadToGitHub)
            await UploadToGitHubAsync();
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

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DeployKit");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GitHubToken);

            var createUrl = $"https://api.github.com/repos/{repo}/releases";
            var createBody = JsonSerializer.Serialize(new
            {
                tag_name = tag,
                name = tag,
                body = ReleaseNotes,
                prerelease = false
            });
            var createResponse = await client.PostAsync(createUrl,
                new StringContent(createBody, Encoding.UTF8, "application/json"));
            createResponse.EnsureSuccessStatusCode();
            var createResult = JsonSerializer.Deserialize<JsonElement>(
                await createResponse.Content.ReadAsStringAsync());

            var uploadUrl = createResult.GetProperty("upload_url").GetString()!
                .Replace("{?name,label}", $"?name={zipName}");
            var releaseUrl = createResult.GetProperty("html_url").GetString()!;

            await using var fileStream = File.OpenRead(FilePath);
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            var uploadResponse = await client.PostAsync(uploadUrl, fileContent);
            uploadResponse.EnsureSuccessStatusCode();

            var assetResult = JsonSerializer.Deserialize<JsonElement>(
                await uploadResponse.Content.ReadAsStringAsync());
            var downloadUrl = assetResult.GetProperty("browser_download_url").GetString()!;

            DownloadUrl = downloadUrl;
            StatusMessage = $"🐙✅ تم الرفع! {releaseUrl}";
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
            return JsonSerializer.Deserialize<CloudSettings>(json);
        }
        catch { return null; }
    }

    private class CloudSettings
    {
        public string? GitHubUser { get; set; }
        public string? GitHubRepo { get; set; }
        public string? GitHubToken { get; set; }
    }
}
