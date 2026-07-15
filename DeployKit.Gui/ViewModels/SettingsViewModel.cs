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

    private string _cloudUrl = "http://localhost:5000";
    public string CloudUrl
    {
        get => _cloudUrl;
        set => SetProperty(ref _cloudUrl, value);
    }

    private string _apiKey = "";
    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    private string _adminToken = "";
    public string AdminToken
    {
        get => _adminToken;
        set => SetProperty(ref _adminToken, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string AppVersion { get; } = typeof(SettingsViewModel).Assembly.GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "1.0.0";

    public RelayCommand SaveSettingsCommand { get; }

    public SettingsViewModel()
    {
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
                    GitHubUser = data.GitHubUser ?? "";
                    GitHubRepo = data.GitHubRepo ?? "";
                    GitHubToken = SecureStorage.Decrypt(data.GitHubTokenEncrypted) ?? "";
                    CloudUrl = data.CloudUrl ?? "http://localhost:5000";
                    ApiKey = SecureStorage.Decrypt(data.ApiKeyEncrypted) ?? "";
                    AdminToken = SecureStorage.Decrypt(data.AdminTokenEncrypted) ?? "";
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
                GitHubUser = GitHubUser,
                GitHubRepo = GitHubRepo,
                GitHubTokenEncrypted = SecureStorage.Encrypt(GitHubToken),
                CloudUrl = CloudUrl,
                ApiKeyEncrypted = SecureStorage.Encrypt(ApiKey),
                AdminTokenEncrypted = SecureStorage.Encrypt(AdminToken)
            };
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(data));
            StatusMessage = "✅ تم حفظ الإعدادات";
        }
        catch { }
    }

    internal class SettingsData
    {
        public string? CloudUrl { get; set; }
        public string? GitHubUser { get; set; }
        public string? GitHubRepo { get; set; }
        public string? GitHubToken { get; set; }
        public string? GitHubTokenEncrypted { get; set; }
        public string? GitHubRawUrl { get; set; }
        public string? ApiKeyEncrypted { get; set; }
        public string? AdminTokenEncrypted { get; set; }
    }
}
