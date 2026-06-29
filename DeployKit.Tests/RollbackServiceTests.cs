using DeployKit.Core.Services;

namespace DeployKit.Tests;

public class RollbackServiceTests
{
    [Fact]
    public void CreateBackupAndRestore()
    {
        using var appDir = new TempDir();
        var backupDir = Path.Combine(Path.GetTempPath(), $"DeployKitRollback_{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupDir);
        try
        {
            var svc = new RollbackService(backupDir);
            var filePath = Path.Combine(appDir.Path, "test.txt");
            File.WriteAllText(filePath, "original content");

            var backupPath = svc.CreateBackup(appDir.Path);
            Assert.True(Directory.Exists(backupPath));
            Assert.True(File.Exists(Path.Combine(backupPath, "test.txt")));

            File.WriteAllText(filePath, "modified content");
            svc.Restore(backupPath, appDir.Path);

            Assert.Equal("original content", File.ReadAllText(filePath));
        }
        finally
        {
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);
        }
    }
}
