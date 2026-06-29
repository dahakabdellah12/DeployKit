using System.Windows;
using DeployKit.Gui.Helpers;

namespace DeployKit.Gui;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _ = ThemeService.Instance;
    }
}
