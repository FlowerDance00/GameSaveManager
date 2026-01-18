using GameSaveManager.Core.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace GameSaveManager.Core.Services;

public class SettingsService
{
    private const string FileName = "config.json";
    private const string PortableFlag = "portable.flag";
    private const string PortableDataFolder = "Data";

    private static bool IsPackaged()
    {
        try
        {
            _ = ApplicationData.Current.LocalFolder; // packaged 时可用
            return true;
        }
        catch
        {
            return false; // unpackaged 时可能异常
        }
    }

    private static string GetExeDir()
    {
        // WinUI 3 桌面一般可用
        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static bool IsPortableEnabled()
    {
        // 打包应用（MSIX）安装目录不可写，便携模式无意义，直接禁用
        if (IsPackaged()) return false;

        var exeDir = GetExeDir();
        var flagPath = Path.Combine(exeDir, PortableFlag);
        return File.Exists(flagPath);
    }

    private static string GetConfigDir()
    {
        if (IsPortableEnabled())
        {
            var exeDir = GetExeDir();
            var dir = Path.Combine(exeDir, PortableDataFolder);
            Directory.CreateDirectory(dir);
            return dir;
        }

        // MSIX 打包运行：LocalState
        try
        {
            var localFolder = ApplicationData.Current.LocalFolder.Path;
            Directory.CreateDirectory(localFolder);
            return localFolder;
        }
        catch
        {
            // 非打包运行：LocalAppData\GameSaveManager
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameSaveManager"
            );
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string GetConfigPath()
    {
        return Path.Combine(GetConfigDir(), FileName);
    }

    public async Task<AppSettings> LoadAsync()
    {
        var path = GetConfigPath();

        if (!File.Exists(path))
        {
            var settings = new AppSettings();
            await SaveAsync(settings);
            return settings;
        }

        var json = await File.ReadAllTextAsync(path);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json);

        return loaded ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var path = GetConfigPath();
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(path, json);
    }

    // 给 UI 用
    public string ConfigPath => GetConfigPath();
    public string ConfigDir => GetConfigDir();

    // 可选：给 UI 显示当前模式
    public bool PortableMode => IsPortableEnabled();
}