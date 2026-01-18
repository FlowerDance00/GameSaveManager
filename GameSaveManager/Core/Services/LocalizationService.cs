// File: Core/Services/LocalizationService.cs
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameSaveManager.Core.Services
{
    /// <summary>
    /// Simple localization service (Chinese + English) with easy extensibility.
    /// - No hardcoded UI strings in XAML/code-behind: use L("Key") / LF("Key", args)
    /// - Supports "auto" language (follow OS UI culture)
    /// - Raise LanguageChanged when language switches so UI can refresh
    /// </summary>
    public class LocalizationService
    {
        public const string LangAuto = "auto";
        public const string LangZhCN = "zh-CN";
        public const string LangEnUS = "en-US";

        private string _language = LangAuto;

        // Two language dictionaries (MVP). You can add more languages later.
        private readonly Dictionary<string, string> _zh = new(StringComparer.OrdinalIgnoreCase)
        {
            // App / Sections
            ["App.Title"] = "游戏存档管理器",
            ["Section.Games"] = "游戏列表",
            ["Section.Details"] = "详情",
            ["Section.Settings"] = "设置",
            ["Section.Backups"] = "备份记录",

            // Game list
            ["GameList.Empty"] = "尚未添加游戏",
            ["GameList.Add"] = "添加游戏",
            ["GameList.LastBackup"] = "上次备份",
            ["GameList.NoBackup"] = "尚未备份",

            // Game details
            ["Game.SelectHint"] = "请从左侧选择一个游戏",
            ["Game.Name"] = "游戏名称",
            ["Game.SaveFolder"] = "存档目录",
            ["Game.SaveFolder.Empty"] = "尚未设置存档目录",
            ["Game.SaveFolder.Choose"] = "选择",
            ["Game.SaveFolder.Save"] = "保存",

            // Backup
            ["Backup.Root"] = "备份根目录",
            ["Backup.Root.Placeholder"] = "例如：D:\\GameBackups",
            ["Backup.OpenFolder"] = "打开备份目录",
            ["Backup.KeepLatest"] = "仅保留最近的备份",

            ["Backup.Now"] = "立即备份",
            ["Backup.AutoOnStartup"] = "启动时自动备份",
            ["Backup.Starting"] = "正在备份…",
            ["Backup.InProgress"] = "正在备份：{0}",
            ["Backup.Success"] = "备份完成",
            ["Backup.Failed"] = "备份失败",

            // Switch (restore)
            ["Switch.Title"] = "切换存档",
            ["Switch.ToSelected"] = "切换到此备份",
            ["Switch.BeforeBackup"] = "切换前将自动备份当前存档",
            ["Switch.InProgress"] = "正在切换存档…",
            ["Switch.Success"] = "存档切换完成",
            ["Switch.Failed"] = "存档切换失败",

            // Undo
            ["Undo.LastSwitch"] = "撤销上一次切换",
            ["Undo.Success"] = "已恢复到切换前状态",
            ["Undo.Failed"] = "撤销失败",

            // Errors
            ["Error.NoGameSelected"] = "未选择游戏",
            ["Error.SavePathMissing"] = "存档目录不存在",
            ["Error.BackupRootMissing"] = "备份目录未设置",
            ["Error.BackupFailed"] = "备份过程中发生错误",
            ["Error.SwitchFailed"] = "切换过程中发生错误",
            ["Error.GameRunning"] = "检测到游戏正在运行，请先关闭",

            // Common buttons
            ["Common.OK"] = "确定",
            ["Common.Cancel"] = "取消",
            ["Common.Yes"] = "是",
            ["Common.No"] = "否",
            ["Common.Close"] = "关闭",

            // Language
            ["Language.Label"] = "界面语言",
            ["Language.Auto"] = "跟随系统",
            ["Language.Chinese"] = "中文",
            ["Language.English"] = "English",
            ["Language.RestartHint"] = "语言切换后需重新打开窗口",

            // Status
            ["Status.Ready"] = "就绪",
            ["Status.Working"] = "正在处理…",
            ["Status.Done"] = "已完成",

            // About
            ["About.Title"] = "关于",
            ["About.Summary"] = "本地游戏存档管理工具，用于安全备份与快速切换存档。",
            ["About.Features.Title"] = "核心特性",
            ["About.Features.1"] = "手动与启动自动备份",
            ["About.Features.2"] = "智能备份，仅在存档变化时创建新备份",
            ["About.Features.3"] = "切换存档前自动兜底备份，降低误操作风险",
            ["About.Features.4"] = "支持便携模式，适合云盘同步多设备使用",
            ["About.Features.5"] = "支持中英文界面，并可扩展更多语言",
            ["About.Privacy"] = "所有操作均在本地完成，不会上传任何存档或隐私数据。"
        };

        private readonly Dictionary<string, string> _en = new(StringComparer.OrdinalIgnoreCase)
        {
            // App / Sections
            ["App.Title"] = "Game Save Manager",
            ["Section.Games"] = "Games",
            ["Section.Details"] = "Details",
            ["Section.Settings"] = "Settings",
            ["Section.Backups"] = "Backups",

            // Game list
            ["GameList.Empty"] = "No games added yet",
            ["GameList.Add"] = "Add Game",
            ["GameList.LastBackup"] = "Last Backup",
            ["GameList.NoBackup"] = "No backup yet",

            // Game details
            ["Game.SelectHint"] = "Select a game from the left",
            ["Game.Name"] = "Game Name",
            ["Game.SaveFolder"] = "Save Folder",
            ["Game.SaveFolder.Empty"] = "Save folder not set",
            ["Game.SaveFolder.Choose"] = "Choose",
            ["Game.SaveFolder.Save"] = "Save",

            // Backup
            ["Backup.Root"] = "Backup Root Folder",
            ["Backup.Root.Placeholder"] = "Example: D:\\GameBackups",
            ["Backup.OpenFolder"] = "Open Backup Folder",
            ["Backup.KeepLatest"] = "Keep latest backups",

            ["Backup.Now"] = "Backup Now",
            ["Backup.AutoOnStartup"] = "Auto backup on startup",
            ["Backup.Starting"] = "Backup starting…",
            ["Backup.InProgress"] = "Backing up: {0}",
            ["Backup.Success"] = "Backup completed",
            ["Backup.Failed"] = "Backup failed",

            // Switch (restore)
            ["Switch.Title"] = "Switch Save",
            ["Switch.ToSelected"] = "Switch to this backup",
            ["Switch.BeforeBackup"] = "Current save will be backed up before switching",
            ["Switch.InProgress"] = "Switching save…",
            ["Switch.Success"] = "Save switched successfully",
            ["Switch.Failed"] = "Failed to switch save",

            // Undo
            ["Undo.LastSwitch"] = "Undo last switch",
            ["Undo.Success"] = "Restored to previous state",
            ["Undo.Failed"] = "Undo failed",

            // Errors
            ["Error.NoGameSelected"] = "No game selected",
            ["Error.SavePathMissing"] = "Save folder does not exist",
            ["Error.BackupRootMissing"] = "Backup root folder is not set",
            ["Error.BackupFailed"] = "An error occurred during backup",
            ["Error.SwitchFailed"] = "An error occurred during switching",
            ["Error.GameRunning"] = "Game is running. Please close it first",

            // Common buttons
            ["Common.OK"] = "OK",
            ["Common.Cancel"] = "Cancel",
            ["Common.Yes"] = "Yes",
            ["Common.No"] = "No",
            ["Common.Close"] = "Close",

            // Language
            ["Language.Label"] = "Language",
            ["Language.Auto"] = "Follow system",
            ["Language.Chinese"] = "中文",
            ["Language.English"] = "English",
            ["Language.RestartHint"] = "Restart window to apply language",

            // Status
            ["Status.Ready"] = "Ready",
            ["Status.Working"] = "Working…",
            ["Status.Done"] = "Done",

            // About
            ["About.Title"] = "About",
            ["About.Summary"] = "A local tool for safely backing up and switching game save files.",
            ["About.Features.Title"] = "Key Features",
            ["About.Features.1"] = "Manual and auto backup on startup",
            ["About.Features.2"] = "Smart backup only when saves change",
            ["About.Features.3"] = "Safety backup before switching to reduce overwrite risk",
            ["About.Features.4"] = "Portable mode for cloud-synced multi-device workflow",
            ["About.Features.5"] = "Bilingual UI with extensible localization",
            ["About.Privacy"] = "All operations are local. No save data or personal information is uploaded."
        };

        public event EventHandler? LanguageChanged;

        /// <summary>
        /// "auto" | "zh-CN" | "en-US"
        /// </summary>
        public string Language => _language;

        /// <summary>
        /// Set language and notify UI to refresh.
        /// </summary>
        public void SetLanguage(string? language)
        {
            var next = NormalizeLanguage(language);
            if (string.Equals(_language, next, StringComparison.OrdinalIgnoreCase))
                return;

            _language = next;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Get localized string by key.
        /// If missing, returns the key itself (so you can spot missing entries).
        /// </summary>
        public string L(string key)
        {
            var dict = ResolveDictionary();
            if (dict.TryGetValue(key, out var val))
                return val;

            // Fallback: try the other language
            var fallback = dict == _zh ? _en : _zh;
            if (fallback.TryGetValue(key, out var val2))
                return val2;

            return key;
        }

        /// <summary>
        /// Get formatted localized string.
        /// Example: LF("Backup.InProgress", gameName)
        /// </summary>
        public string LF(string key, params object[] args)
        {
            var format = L(key);
            try
            {
                return string.Format(CultureInfo.CurrentCulture, format, args);
            }
            catch
            {
                // If format placeholders mismatch, return raw format + args for visibility
                return format + " " + string.Join(" ", args);
            }
        }

        /// <summary>
        /// Helper: decide initial language from config + OS UI culture.
        /// Call this once after loading settings.
        /// </summary>
        public string DecideInitialLanguage(string? configLanguage)
        {
            var normalized = NormalizeLanguage(configLanguage);

            if (!string.Equals(normalized, LangAuto, StringComparison.OrdinalIgnoreCase))
                return normalized;

            // Follow OS UI language
            var ui = CultureInfo.CurrentUICulture?.Name ?? "";
            if (ui.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return LangZhCN;

            return LangEnUS;
        }

        private Dictionary<string, string> ResolveDictionary()
        {
            var lang = NormalizeLanguage(_language);
            if (string.Equals(lang, LangAuto, StringComparison.OrdinalIgnoreCase))
            {
                var ui = CultureInfo.CurrentUICulture?.Name ?? "";
                if (ui.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                    return _zh;

                return _en;
            }

            if (string.Equals(lang, LangZhCN, StringComparison.OrdinalIgnoreCase))
                return _zh;

            return _en;
        }

        private static string NormalizeLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return LangAuto;

            var v = language.Trim();

            // Common aliases
            if (v.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("zh-hans", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("zh-cn", StringComparison.OrdinalIgnoreCase))
                return LangZhCN;

            if (v.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("en-us", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("en-gb", StringComparison.OrdinalIgnoreCase))
                return LangEnUS;

            if (v.Equals(LangAuto, StringComparison.OrdinalIgnoreCase))
                return LangAuto;

            // Accept raw IETF tag if user types it; fallback will handle.
            return v;
        }
    }
}
