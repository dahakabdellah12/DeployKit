using System.Windows;

namespace DeployKit.TestApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Integration.DeployKit.Configure("e2d0fd89bea8", "http://localhost:5000");
    }
}
