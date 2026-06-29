using System.Windows;

namespace DeployKit.TestApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Integration.DeployKit.Configure("test_app_key", "http://localhost:5000");
    }
}
