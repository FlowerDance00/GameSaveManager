using GameSaveManager.Core.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameSaveManager.Core.Services;

public class BackupService
{
    public async Task<string> BackupGameAsync(Game game, string backupRootDir)
    {
        var savePath = PathTemplateResolver.Expand(game.SavePathTemplate ?? game.SavePath ?? "");

        if (string.IsNullOrWhiteSpace(savePath))
            throw new InvalidOperationException("Save folder is not set.");

        if (!Directory.Exists(savePath))
            throw new DirectoryNotFoundException($"Save folder not found: {savePath}");

        if (string.IsNullOrWhiteSpace(backupRootDir))
            throw new InvalidOperationException("Backup root folder is not set.");

        Directory.CreateDirectory(backupRootDir);

        var safeName = MakeSafeFolderName(game.Name);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        var destDir = Path.Combine(backupRootDir, safeName, timestamp);
        Directory.CreateDirectory(destDir);

        var copied = await Task.Run(() => CopyDirectory(savePath, destDir));
        if (copied == 0)
            throw new InvalidOperationException($"Backup folder created but no files were copied. Source: {savePath}");

        return destDir;
    }

    private static int CopyDirectory(string sourceDir, string destDir)
    {
        int copiedCount = 0;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };

        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", options))
        {
            try
            {
                var rel = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(destDir, rel);

                var parent = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                File.Copy(file, targetFile, overwrite: true);
                copiedCount++;
            }
            catch
            {
                // 单个文件失败不影响整体
            }
        }

        return copiedCount;
    }

    private static string MakeSafeFolderName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }

    public async Task RestoreGameAsync(Game game, string backupVersionDir)
    {
        if (string.IsNullOrWhiteSpace(game.SavePath))
            throw new InvalidOperationException("Save folder is not set.");

        if (!Directory.Exists(game.SavePath))
            Directory.CreateDirectory(game.SavePath);

        if (!Directory.Exists(backupVersionDir))
            throw new DirectoryNotFoundException($"Backup version folder not found: {backupVersionDir}");

        // 先清空当前存档目录，再复制备份内容进去
        await Task.Run(() =>
        {
            DeleteDirectoryContents(game.SavePath!);
            CopyDirectory(backupVersionDir, game.SavePath!);
        });
    }

    private static void DeleteDirectoryContents(string dir)
    {
        var di = new DirectoryInfo(dir);

        foreach (var file in di.GetFiles())
        {
            file.IsReadOnly = false;
            file.Delete();
        }

        foreach (var sub in di.GetDirectories())
        {
            sub.Delete(true);
        }
    }
    public async Task<(bool backedUp, string? destDir, string newFingerprint)> BackupGameIfChangedAsync(Game game, string backupRootDir)
    {
        if (string.IsNullOrWhiteSpace(game.SavePath))
            throw new InvalidOperationException("Save folder is not set.");

        if (!Directory.Exists(game.SavePath))
            throw new DirectoryNotFoundException($"Save folder not found: {game.SavePath}");

        if (string.IsNullOrWhiteSpace(backupRootDir))
            throw new InvalidOperationException("Backup root folder is not set.");

        // 1) 先检查：有没有任何备份版本。没有的话，必须先备份一次
        var safeName = MakeSafeFolderName(game.Name);
        var gameBackupDir = Path.Combine(backupRootDir, safeName);

        bool hasAnyBackup = Directory.Exists(gameBackupDir) &&
                            Directory.EnumerateDirectories(gameBackupDir).Take(1).Any();

        string newFp;
        try
        {
            newFp = await Task.Run(() => ComputeFingerprint(game.SavePath!));
        }
        catch
        {
            // 指纹失败就强制备份
            var destFallback = await BackupGameAsync(game, backupRootDir);
            return (true, destFallback, "");
        }

        // 2) 如果还没有任何备份版本，即使指纹相同也强制备份一次
        if (!hasAnyBackup)
        {
            var dest = await BackupGameAsync(game, backupRootDir);
            return (true, dest, newFp);
        }

        // 3) 有备份版本时才允许“无变化跳过”
        if (!string.IsNullOrWhiteSpace(game.SaveFingerprint) && game.SaveFingerprint == newFp)
        {
            return (false, null, newFp);
        }

        var dest2 = await BackupGameAsync(game, backupRootDir);
        return (true, dest2, newFp);
    }

    private static string ComputeFingerprint(string saveDir)
    {
        long totalBytes = 0;
        long fileCount = 0;
        long latestTicks = 0;

        // .NET 6+ 支持 EnumerationOptions，IgnoreInaccessible 很关键
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };

        foreach (var file in Directory.EnumerateFiles(saveDir, "*", options))
        {
            try
            {
                var fi = new FileInfo(file);
                fileCount++;
                totalBytes += fi.Length;

                var t = fi.LastWriteTimeUtc.Ticks;
                if (t > latestTicks) latestTicks = t;
            }
            catch
            {
                // 单文件读取失败忽略
            }
        }

        return $"{fileCount}|{totalBytes}|{latestTicks}";
    }
    public async Task BackupAsync(Game game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        if (string.IsNullOrWhiteSpace(game.SavePath))
            throw new InvalidOperationException("Game save path is empty.");

        if (!Directory.Exists(game.SavePath))
            throw new DirectoryNotFoundException(
                $"Save path does not exist: {game.SavePath}");

        // 这里先模拟一个“耗时备份动作”
        await Task.Delay(1000);

        // 后续我们会在这里实现：
        // - 创建备份目录
        // - 复制文件
        // - 压缩成 zip
    }
    public void CleanupOldBackups(Game game, string backupRootDir, int keepLastN)
    {
        if (keepLastN <= 0) return;

        var safeName = MakeSafeFolderName(game.Name);
        var gameBackupDir = Path.Combine(backupRootDir, safeName);
        if (!Directory.Exists(gameBackupDir)) return;

        var dirs = Directory.GetDirectories(gameBackupDir)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(di => di.CreationTimeUtc)
            .ToList();

        if (dirs.Count <= keepLastN) return;

        foreach (var old in dirs.Skip(keepLastN))
        {
            try
            {
                old.Delete(true);
            }
            catch
            {
                // 删除失败就跳过，不影响主流程
            }
        }
    }
}