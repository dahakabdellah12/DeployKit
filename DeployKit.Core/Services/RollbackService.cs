namespace DeployKit.Core.Services;

public class RollbackService
{
    public string BackupDir { get; }

    public RollbackService(string backupDir)
    {
        BackupDir = backupDir;
    }

    public string CreateBackup(string targetDir)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var backupPath = Path.Combine(BackupDir, $"{Path.GetFileName(targetDir)}_{timestamp}");
        Directory.CreateDirectory(backupPath);

        foreach (var file in Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(targetDir, file);
            var dest = Path.Combine(backupPath, relative);
            var destDir = Path.GetDirectoryName(dest)!;
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);
            File.Copy(file, dest, overwrite: true);
        }

        return backupPath;
    }

    public void Restore(string backupPath, string targetDir)
    {
        foreach (var file in Directory.EnumerateFiles(backupPath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(backupPath, file);
            var dest = Path.Combine(targetDir, relative);
            var destDir = Path.GetDirectoryName(dest)!;
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);
            File.Copy(file, dest, overwrite: true);
        }
    }

    public void Cleanup(string backupPath)
    {
        if (Directory.Exists(backupPath))
            Directory.Delete(backupPath, recursive: true);
    }
}
