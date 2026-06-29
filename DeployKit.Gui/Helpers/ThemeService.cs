using System.Windows;
using System.Windows.Media;

namespace DeployKit.Gui.Helpers;

public class ThemeService
{
    private static readonly ResourceDictionary LightTheme = new() { Source = new Uri("Themes/Light.xaml", UriKind.Relative) };
    private static readonly ResourceDictionary DarkTheme = new() { Source = new Uri("Themes/Dark.xaml", UriKind.Relative) };

    public static ThemeService Instance { get; } = new();

    public bool IsDark { get; private set; }

    public event Action? ThemeChanged;

    public void Toggle()
    {
        IsDark = !IsDark;
        ApplyTheme();
        ThemeChanged?.Invoke();
    }

    public void SetDark(bool dark)
    {
        if (IsDark == dark) return;
        IsDark = dark;
        ApplyTheme();
        ThemeChanged?.Invoke();
    }

    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        var dict = app.Resources.MergedDictionaries;
        dict.Clear();

        dict.Add(LightTheme);
        if (IsDark) dict.Add(DarkTheme);
    }

    public Brush GetBrush(string key) =>
        Application.Current.TryFindResource(key) is Brush brush ? brush : Brushes.Transparent;
}
