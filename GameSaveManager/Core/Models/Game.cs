using System;
namespace GameSaveManager.Core.Models;

public class Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string? ExePath { get; set; }      // 未来启动器用，现在可以为空
    public string? SavePath { get; set; }     // 存档路径，用户手动设置
    public DateTimeOffset? LastBackupTime { get; set; }

    public string? SaveFingerprint { get; set; }  // 用于“无变化不备份”
    public string? SavePathTemplate { get; set; }  // 如 %DOCUMENTS%\My Games\GameX
}