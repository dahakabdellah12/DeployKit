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
        set => SetProperty(ref _apiKey, value);
    }

    private string _cloudUrl = "http://localhost:5000";
    public string CloudUrl
    {
        get => _cloudUrl;
        set => SetProperty(ref _cloudUrl, value);
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

    public RelayCommandAsync RegisterCommand { get; }
    public RelayCommand CopyKeyCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }

    public SettingsViewModel()
    {
        RegisterCommand = new RelayCommandAsync(async _ => await RegisterAsync());
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
                AppName = AppName
            };
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(data));
        }
        catch { }
    }

    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(AppName))
        {
            StatusMessage = "الرجاء إدخال اسم التطبيق";
            return;
        }

        StatusMessage = "جاري التسجيل...";
        try
        {
            var cloud = new CloudService(CloudUrl);
            var result = await cloud.RegisterAsync(AppName);
            ApiKey = result.AppKey;
            HasApiKey = true;
            StatusMessage = $"✅ تم التسجيل! المفتاح: {result.AppKey}";
            SaveSettings();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ فشل التسجيل: {ex.Message}";
        }
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
    }
}
