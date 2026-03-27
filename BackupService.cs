using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class BackupService
{
    private readonly LoggerService _logger;
    private int _totalCopiedFiles;
    private int _totalFailedFiles;

    public BackupService(LoggerService logger = null)
    {
        _logger = logger ?? new LoggerService();
        _totalCopiedFiles = 0;
        _totalFailedFiles = 0;
    }

    public CopyResult PerformBackup(string sourcePath, string destinationPath)
    {
        var result = new CopyResult();
        try
        {
            if (!Directory.Exists(sourcePath))
                throw new DirectoryNotFoundException("Source directory does not exist.");

            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);

            var files = Directory.GetFiles(sourcePath);
            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileName(file);
                    var destFile = Path.Combine(destinationPath, fileName);
                    File.Copy(file, destFile, true);
                    _totalCopiedFiles++;
                    _logger?.LogInfo($"Successfully copied: {fileName}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to copy {file}: {ex.Message}");
                    _totalFailedFiles++;
                }
            }

            _logger?.LogInfo($"Backup completed. Total copied files: {_totalCopiedFiles} , Total failed files: {_totalFailedFiles}");

            result.TotalCopiedFiles = _totalCopiedFiles;
            result.TotalFailedFiles = _totalFailedFiles;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during backup: {ex.Message}");
            throw;
        }
        return result;
    }

    public void CleanUpBackup(string backupPath)
    {
        try
        {
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
                _logger?.LogInfo($"Cleaned up backup at {backupPath}");
            }
            else
            {
                _logger?.LogWarning($"Date to clean up does not exist: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during cleanup: {ex.Message}");
            throw;
        }
    }
}

public class CopyResult
{
    public int TotalCopiedFiles { get; set; }
    public int TotalFailedFiles { get; set; }
}

public class LoggerService
{
    public void LogInfo(string message)
    {
        // Implementation for logging info
        Console.WriteLine($"INFO: {message}");
    }

    public void LogError(string message)
    {
        // Implementation for logging errors
        Console.WriteLine($"ERROR: {message}");
    }

    public void LogWarning(string message)
    {
        // Implementation for logging warnings
        Console.WriteLine($"WARNING: {message}");
    }
}