using System;
using System.Collections.Generic;
using System.Linq; // 添加此 using 指令
using System.Runtime.InteropServices;

namespace GameSaveManager.Core.Services;

public static class PathTemplateResolver
{
    private record Token(string Key, string Value, int Priority);

    // Priority 数字越小越优先（更希望被选中）
    private static List<Token> BuildTokens()
    {
        var list = new List<Token>
        {
            new("%SAVEDGAMES%",  GetSavedGamesFolder(), 0),
            new("%DOCUMENTS%",   Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 1),
            new("%APPDATA%",     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 2),
            new("%LOCALAPPDATA%",Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 3),
            new("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 9),
        };

        // 过滤掉获取失败的 token
        return list.Where(t => !string.IsNullOrWhiteSpace(t.Value)).ToList();
    }

    public static string Expand(string templateOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(templateOrAbsolute)) return "";

        var s = templateOrAbsolute;

        foreach (var t in BuildTokens())
            s = s.Replace(t.Key, t.Value, StringComparison.OrdinalIgnoreCase);

        // 也支持系统环境变量，例如 %TEMP%
        s = Environment.ExpandEnvironmentVariables(s);

        return s;
    }

    /// <summary>
    /// 智能把绝对路径压缩成模板路径：最长匹配 + 优先级。
    /// </summary>
    public static string ToBestTemplate(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath)) return "";

        var abs = Normalize(absolutePath);

        var tokens = BuildTokens()
            // 最长匹配优先，长度相同按 Priority 更高（数字更小）优先
            .OrderByDescending(t => Normalize(t.Value).Length)
            .ThenBy(t => t.Priority)
            .ToList();

        foreach (var t in tokens)
        {
            var root = Normalize(t.Value);

            if (abs.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(abs, root, StringComparison.OrdinalIgnoreCase))
            {
                var rest = abs.Length == root.Length ? "" : abs.Substring(root.Length).TrimStart('\\');
                return string.IsNullOrEmpty(rest) ? t.Key : $"{t.Key}\\{rest}";
            }
        }

        // 找不到匹配 token 就存绝对路径（兜底）
        return absolutePath;
    }

    private static string Normalize(string p) => p.Trim().TrimEnd('\\');

    private static string GetSavedGamesFolder()
    {
        try
        {
            // FOLDERID_SavedGames
            Guid id = new("4C5C32FF-BB9D-43B0-BF5A-1E1FAF1B4B6B");
            SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var p);
            var path = Marshal.PtrToStringUni(p) ?? "";
            Marshal.FreeCoTaskMem(p);
            return path;
        }
        catch { return ""; }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}