using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DeployKit.Gui.Helpers;

namespace DeployKit.Gui.ViewModels;

public class HomeViewModel : BaseViewModel
{
    private readonly MainViewModel _main;

    private static readonly string RecentFile =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeployKit", "recent.json");

    public ObservableCollection<RecentProject> RecentProjects { get; } = [];

    private bool _hasRecentProjects;
    public bool HasRecentProjects
    {
        get => _hasRecentProjects;
        set => SetProperty(ref _hasRecentProjects, value);
    }

    private string _statusText = "جاهز للعمل";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public RelayCommand ShowBuildCommand { get; }
    public RelayCommand ShowApplyCommand { get; }
    public RelayCommand ClearRecentCommand { get; }

    public HomeViewModel(MainViewModel main)
    {
        _main = main;

        ShowBuildCommand = new RelayCommand(_ => _main.NavigateTo(1));
        ShowApplyCommand = new RelayCommand(_ => _main.NavigateTo(2));
        ClearRecentCommand = new RelayCommand(_ => ClearRecent());

        LoadRecent();
    }

    public void AddRecent(string name, string fromVer, string toVer, string oldDir, string newDir, string output)
    {
        var existing = RecentProjects.FirstOrDefault(p =>
            p.Name == name && p.FromVersion == fromVer && p.ToVersion == toVer);
        if (existing != null)
            RecentProjects.Remove(existing);

        RecentProjects.Insert(0, new RecentProject
        {
            Name = name,
            FromVersion = fromVer,
            ToVersion = toVer,
            OldDir = oldDir,
            NewDir = newDir,
            OutputPath = output,
            LastUsed = DateTime.Now
        });

        while (RecentProjects.Count > 10)
            RecentProjects.RemoveAt(RecentProjects.Count - 1);

        HasRecentProjects = RecentProjects.Count > 0;
        SaveRecent();
    }

    private void ClearRecent()
    {
        RecentProjects.Clear();
        HasRecentProjects = false;
        SaveRecent();
    }

    private void LoadRecent()
    {
        try
        {
            var dir = Path.GetDirectoryName(RecentFile);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(RecentFile))
            {
                var json = File.ReadAllText(RecentFile);
                var items = JsonSerializer.Deserialize<List<RecentProject>>(json);
                if (items != null)
                {
                    RecentProjects.Clear();
                    foreach (var item in items.OrderByDescending(p => p.LastUsed).Take(10))
                        RecentProjects.Add(item);
                }
            }
        }
        catch
        {
            // ignore
        }
        HasRecentProjects = RecentProjects.Count > 0;
    }

    private void SaveRecent()
    {
        try
        {
            var json = JsonSerializer.Serialize(RecentProjects.ToList());
            var dir = Path.GetDirectoryName(RecentFile);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(RecentFile, json);
        }
        catch
        {
            // ignore
        }
    }
}
