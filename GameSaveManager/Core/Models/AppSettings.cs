using System.Collections.Generic;
namespace GameSaveManager.Core.Models;

public class AppSettings
{
    public string BackupRootDir { get; set; } = "";  // 备份根目录
    public bool AutoBackupOnStartup { get; set; } = true;
    public int KeepLastBackups { get; set; } = 2;
    public List<Game> Games { get; set; } = new();
}