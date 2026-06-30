using System.IO;
using System.Windows;

namespace DeployKit.Updater;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            var logDir = Path.Combine(Path.GetTempPath(), "DeployKit", "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, $"updater_{Environment.ProcessId}.log");
            File.WriteAllText(logPath,
                $"Started at {DateTime.Now:HH:mm:ss.fff}\n" +
                $"Args: {string.Join(" | ", Environment.GetCommandLineArgs())}\n");
        }
        catch { }
    }
}
