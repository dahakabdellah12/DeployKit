using System.Diagnostics;
using DeployKit.Core.Services;

var zipPath = GetArg("--zip");
var targetDir = GetArg("--target");
var appName = GetArg("--app");

if (string.IsNullOrEmpty(zipPath) || string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(appName))
{
    Console.Error.WriteLine("Usage: updater --zip <path> --target <dir> --app <exe>");
    Environment.Exit(1);
}

if (!int.TryParse(GetArg("--pid"), out var pid))
{
    Console.Error.WriteLine("Invalid or missing --pid");
    Environment.Exit(1);
}

try
{
    var process = Process.GetProcessById(pid);
    if (process != null && !process.HasExited)
        process.WaitForExit(30_000);
}
catch (ArgumentException) { }

await Task.Delay(1000);

Console.WriteLine($"Applying update from {zipPath} to {targetDir}...");
var backupDir = Path.Combine(Path.GetTempPath(), "DeployKit", "Backups");
Directory.CreateDirectory(backupDir);

var applier = new PackageApplier(backupDir);
await applier.ApplyAsync(zipPath, targetDir);

Console.WriteLine("Update applied! Restarting app...");
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

static string GetArg(string name)
{
    var args = Environment.GetCommandLineArgs();
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "";
}
