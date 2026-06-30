using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DeployKit.Gui.Helpers;

namespace DeployKit.Gui.ViewModels;

public class HomeViewModel : BaseViewModel
{
    private readonly MainViewModel _main;

    private static readonly string HistoryFile =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeployKit", "releases.json");

    public ObservableCollection<ReleaseRecord> Releases { get; } = [];

    private bool _hasReleases;
    public bool HasReleases
    {
        get => _hasReleases;
        set => SetProperty(ref _hasReleases, value);
    }

    private int _totalReleases;
    public int TotalReleases
    {
        get => _totalReleases;
        set => SetProperty(ref _totalReleases, value);
    }

    private string _totalSize = "";
    public string TotalSize
    {
        get => _totalSize;
        set => SetProperty(ref _totalSize, value);
    }

    private int _cloudCount;
    public int CloudCount
    {
        get => _cloudCount;
        set => SetProperty(ref _cloudCount, value);
    }

    public RelayCommand ShowBuildCommand { get; }
    public RelayCommand ClearHistoryCommand { get; }
    public RelayCommand OpenZipCommand { get; }

    public HomeViewModel(MainViewModel main)
    {
        _main = main;

        ShowBuildCommand = new RelayCommand(_ => _main.NavigateTo(1));
        ClearHistoryCommand = new RelayCommand(_ => ClearHistory());
        OpenZipCommand = new RelayCommand(r => { if (r is ReleaseRecord rec) OpenZip(rec); });

        LoadHistory();
    }

    public void AddRelease(ReleaseRecord record)
    {
        var existing = Releases.FirstOrDefault(r =>
            r.AppName == record.AppName && r.FromVersion == record.FromVersion && r.ToVersion == record.ToVersion);
        if (existing != null)
            Releases.Remove(existing);

        Releases.Insert(0, record);

        while (Releases.Count > 50)
            Releases.RemoveAt(Releases.Count - 1);

        UpdateStats();
        SaveHistory();
    }

    private void ClearHistory()
    {
        Releases.Clear();
        UpdateStats();
        SaveHistory();
    }

    private void OpenZip(ReleaseRecord r)
    {
        if (File.Exists(r.ZipPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = r.ZipPath,
                UseShellExecute = true
            });
    }

    private void UpdateStats()
    {
        TotalReleases = Releases.Count;
        TotalSize = Releases.Where(r => r.FileSize > 0)
            .Sum(r => r.FileSize) switch
        {
            < 1024 => "0 B",
            < 1024 * 1024 => $"{Releases.Where(r => r.FileSize > 0).Sum(r => r.FileSize) / 1024.0:F0} KB",
            _ => $"{Releases.Where(r => r.FileSize > 0).Sum(r => r.FileSize) / (1024.0 * 1024):F1} MB"
        };
        CloudCount = Releases.Count(r => r.IsRegisteredInCloud);
        HasReleases = Releases.Count > 0;
    }

    private void LoadHistory()
    {
        try
        {
            var dir = Path.GetDirectoryName(HistoryFile);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(HistoryFile))
            {
                var json = File.ReadAllText(HistoryFile);
                var items = JsonSerializer.Deserialize<List<ReleaseRecord>>(json);
                if (items != null)
                {
                    Releases.Clear();
                    foreach (var item in items.OrderByDescending(r => r.CreatedAt))
                        Releases.Add(item);
                }
            }
        }
        catch { }

        UpdateStats();
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(Releases.ToList());
            var dir = Path.GetDirectoryName(HistoryFile);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(HistoryFile, json);
        }
        catch { }
    }
}
