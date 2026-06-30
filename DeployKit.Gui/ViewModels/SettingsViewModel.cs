using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DeployKit.Gui.Helpers;

namespace DeployKit.Gui.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeployKit", "settings.json");

    private string _appName = "";
    public string AppName
    {
        get => _appName;
        set => SetProperty(ref _appName, value);
    }

    private string _apiKey = "";
    public string ApiKey
    {
        get => _apiKey;
        set { SetProperty(ref _apiKey, value); HasApiKey = !string.IsNullOrEmpty(value); }
    }

    private string _cloudUrl = "https://deploykit-gb81.onrender.com";
    public string CloudUrl
    {
        get => _cloudUrl;
        set => SetProperty(ref _cloudUrl, value);
    }

    private string _gitHubUser = "";
    public string GitHubUser
    {
        get => _gitHubUser;
        set => SetProperty(ref _gitHubUser, value);
    }

    private string _gitHubRepo = "";
    public string GitHubRepo
    {
        get => _gitHubRepo;
        set => SetProperty(ref _gitHubRepo, value);
    }

    private string _gitHubToken = "";
    public string GitHubToken
    {
        get => _gitHubToken;
        set => SetProperty(ref _gitHubToken, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _hasApiKey;
    public bool HasApiKey
    {
        get => _hasApiKey;
        set => SetProperty(ref _hasApiKey, value);
    }

    public RelayCommand OpenDashboardCommand { get; }
    public RelayCommand CopyKeyCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }

    public SettingsViewModel()
    {
        OpenDashboardCommand = new RelayCommand(_ => OpenDashboard());
        CopyKeyCommand = new RelayCommand(_ => CopyKey());
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data != null)
                {
                    ApiKey = data.ApiKey ?? "";
                    CloudUrl = data.CloudUrl ?? "http://localhost:5000";
                    AppName = data.AppName ?? "";
                    GitHubUser = data.GitHubUser ?? "";
                    GitHubRepo = data.GitHubRepo ?? "";
                    GitHubToken = data.GitHubToken ?? "";
                    HasApiKey = !string.IsNullOrEmpty(ApiKey);
                }
            }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFile);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var data = new SettingsData
            {
                ApiKey = ApiKey,
                CloudUrl = CloudUrl,
                AppName = AppName,
                GitHubUser = GitHubUser,
                GitHubRepo = GitHubRepo,
                GitHubToken = GitHubToken
            };
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(data));
            StatusMessage = "✅ تم حفظ الإعدادات";
        }
        catch { }
    }

    private void OpenDashboard()
    {
        var url = CloudUrl?.TrimEnd('/') ?? "http://localhost:5000";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CopyKey()
    {
        if (!string.IsNullOrEmpty(ApiKey))
        {
            System.Windows.Clipboard.SetText(ApiKey);
            StatusMessage = "📋 تم نسخ المفتاح";
        }
    }

    private class SettingsData
    {
        public string? ApiKey { get; set; }
        public string? CloudUrl { get; set; }
        public string? AppName { get; set; }
        public string? GitHubUser { get; set; }
        public string? GitHubRepo { get; set; }
        public string? GitHubToken { get; set; }
    }
}
