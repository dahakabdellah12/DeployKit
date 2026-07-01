using DeployKit.Gui.Helpers;

namespace DeployKit.Gui.ViewModels;

public class MainViewModel : BaseViewModel
{
    private object _currentView;
    public object CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    private int _selectedNavIndex;
    public int SelectedNavIndex
    {
        get => _selectedNavIndex;
        set => SetProperty(ref _selectedNavIndex, value);
    }

    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetProperty(ref _isDarkTheme, value);
    }

    public string ThemeIcon => IsDarkTheme ? "☀️" : "🌙";

    public HomeViewModel HomeVM { get; }
    public BuildViewModel BuildVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public UploadViewModel UploadVM { get; }

    public RelayCommand ShowHomeCommand { get; }
    public RelayCommand ShowBuildCommand { get; }
    public RelayCommand ShowUploadCommand { get; }
    public RelayCommand ShowSettingsCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public MainViewModel()
    {
        HomeVM = new HomeViewModel(this);
        BuildVM = new BuildViewModel(HomeVM.AddRelease);
        SettingsVM = new SettingsViewModel();
        UploadVM = new UploadViewModel();
        _currentView = HomeVM;

        ShowHomeCommand = new RelayCommand(_ => NavigateTo(0));
        ShowBuildCommand = new RelayCommand(_ => NavigateTo(1));
        ShowUploadCommand = new RelayCommand(_ => NavigateTo(2));
        ShowSettingsCommand = new RelayCommand(_ => NavigateTo(3));
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());

        ThemeService.Instance.ThemeChanged += OnThemeChanged;
    }

    public void NavigateTo(int index)
    {
        SelectedNavIndex = index;
        CurrentView = index switch
        {
            0 => HomeVM,
            1 => BuildVM,
            2 => UploadVM,
            3 => SettingsVM,
            _ => HomeVM
        };
    }

    private void ToggleTheme()
    {
        ThemeService.Instance.Toggle();
    }

    private void OnThemeChanged()
    {
        IsDarkTheme = ThemeService.Instance.IsDark;
        OnPropertyChanged(nameof(ThemeIcon));
    }
}
