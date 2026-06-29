using System.Diagnostics;
using System.Text.Json;
using DeployKit.Core.Services;

var zipPath = GetArg("--zip");
var targetDir = GetArg("--target");
var appName = GetArg("--app");
var prevVersion = GetArg("--prev");

if (string.IsNullOrEmpty(zipPath) || string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(appName))
{
    Console.Error.WriteLine("Usage: updater --zip <path> --target <dir> --app <exe> [--prev <version>] [--pid <pid>]");
    Environment.Exit(1);
}

if (!int.TryParse(GetArg("--pid"), out var pid))
{
    Console.Error.WriteLine("Error: --pid is required and must be a valid process ID");
    Environment.Exit(1);
}

if (!File.Exists(zipPath))
{
    Console.Error.WriteLine($"Error: Update package not found: {zipPath}");
    Environment.Exit(1);
}

if (!Directory.Exists(targetDir))
{
    Console.Error.WriteLine($"Error: Target directory not found: {targetDir}");
    Environment.Exit(1);
}

try
{
    Console.WriteLine("Waiting for application to exit...");
    var process = Process.GetProcessById(pid);
    if (process != null && !process.HasExited)
    {
        if (!process.WaitForExit(30_000))
        {
            Console.Error.WriteLine("Warning: Application did not exit within 30s, proceeding anyway...");
        }
    }
}
catch (ArgumentException)
{
    Console.WriteLine("Application process already exited.");
}

await Task.Delay(1000);

Console.WriteLine($"Applying update from {zipPath} to {targetDir}...");
var backupDir = Path.Combine(Path.GetTempPath(), "DeployKit", "Backups");
Directory.CreateDirectory(backupDir);

try
{
    var applier = new PackageApplier(backupDir);
    await applier.ApplyAsync(zipPath, targetDir);

    if (applier.BackupPath != null)
    {
        SaveRollbackInfo(applier.BackupPath, targetDir, prevVersion);
    }

    Console.WriteLine("Update applied successfully!");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Update failed: {ex.Message}");
    Environment.Exit(2);
}

Console.WriteLine("Restarting application...");
var appPath = Path.Combine(targetDir, appName);
if (File.Exists(appPath))
{
    Process.Start(new ProcessStartInfo
    {
        FileName = appPath,
        WorkingDirectory = targetDir,
        UseShellExecute = true
    });
}
else
{
    Console.Error.WriteLine($"Warning: Application executable not found: {appPath}");
}

static string GetArg(string name)
{
    var args = Environment.GetCommandLineArgs();
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "";
}

static void SaveRollbackInfo(string backupPath, string targetDir, string? prevVersion)
{
    try
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeployKit", "rollback.json");

        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        var info = new
        {
            BackupPath = backupPath,
            TargetDir = targetDir,
            PreviousVersion = prevVersion ?? "",
            CreatedAt = DateTime.UtcNow
        };

        File.WriteAllText(path, JsonSerializer.Serialize(info));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Could not save rollback info: {ex.Message}");
    }
}
