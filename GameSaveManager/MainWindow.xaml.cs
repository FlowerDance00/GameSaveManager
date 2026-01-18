using GameSaveManager.Core.Models;
using GameSaveManager.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace GameSaveManager;

public sealed partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly ObservableCollection<Game> _games = new();
    private AppSettings _settings = new();
    private readonly BackupService _backupService = new();
    private Game? _selectedGame;
    private bool _isInitializing = true;

    public MainWindow()
    {
        this.InitializeComponent();
        TxtConfigPath.Text = _settingsService.ConfigPath;
        this.Activated += MainWindow_Activated;
        BtnAddGame.Click += BtnAddGame_Click;

        BtnSaveBackupRoot.Click += BtnSaveBackupRoot_Click;
        GameListView.SelectionChanged += GameListView_SelectionChanged;

        BtnPickSaveFolder.Click += BtnPickSaveFolder_Click;
        BtnSaveSaveFolder.Click += BtnSaveSaveFolder_Click;
        BtnBackupNow.Click += BtnBackupNow_Click;

        BtnOpenBackupFolder.Click += BtnOpenBackupFolder_Click;
        BtnRestoreSelected.Click += BtnRestoreSelected_Click;
        BtnRestoreLatest.Click += BtnRestoreLatest_Click;

        BackupListView.SelectionChanged += BackupListView_SelectionChanged;

        TglAutoBackup.Toggled += TglAutoBackup_Toggled;
    }

    private async void BtnAddGame_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox
        {
            PlaceholderText = "Game name",
            MinWidth = 420
        };

        var savePathBox = new TextBox
        {
            PlaceholderText = "Save folder path",
            MinWidth = 420,
            IsReadOnly = true
        };

        var pickBtn = new Button
        {
            Content = "Choose Save Folder"
        };

        pickBtn.Click += async (_, __) =>
        {
            var path = await PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                savePathBox.Text = path;
            }
        };

        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(new TextBlock { Text = "Game Name" });
        layout.Children.Add(nameBox);
        layout.Children.Add(new TextBlock { Text = "Save Folder" });
        layout.Children.Add(savePathBox);
        layout.Children.Add(pickBtn);

        var dialog = new ContentDialog
        {
            Title = "Add Game",
            Content = layout,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var name = nameBox.Text?.Trim() ?? "";
        var savePath = savePathBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            await ShowSimpleDialogAsync("Game name is required.");
            return;
        }

        var game = new Game
        {
            Name = name,
            SavePath = string.IsNullOrWhiteSpace(savePath) ? null : savePath
        };

        // 更新 UI 集合
        _games.Add(game);

        // 同步回 settings 并保存
        _settings.Games = _games.ToList();
        await _settingsService.SaveAsync(_settings);
    }

    private async Task ShowSimpleDialogAsync(string message)
    {
        var d = new ContentDialog
        {
            Title = "Notice",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        await d.ShowAsync();
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        this.Activated -= MainWindow_Activated;

        _isInitializing = true;

        _settings = await _settingsService.LoadAsync();

        TxtBackupRoot.Text = _settings.BackupRootDir;

        // 先解绑或用初始化标记都行，这里用标记
        TglAutoBackup.IsOn = _settings.AutoBackupOnStartup;

        _games.Clear();
        foreach (var g in _settings.Games)
            _games.Add(g);

        GameListView.ItemsSource = _games;

        _isInitializing = false;

        await RunAutoBackupIfEnabledAsync();
    }

    private void GameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedGame = GameListView.SelectedItem as Game;

        if (_selectedGame is null)
        {
            TxtSelectedGame.Text = "Select a game on the left.";
            TxtSavePath.Text = "";
            BackupListView.ItemsSource = null;
            return;
        }

        TxtSelectedGame.Text = _selectedGame.Name;
        TxtSavePath.Text = _selectedGame.SavePath ?? "";

        RefreshBackupList();
    }

    private async void BtnSaveBackupRoot_Click(object sender, RoutedEventArgs e)
    {
        _settings.BackupRootDir = TxtBackupRoot.Text?.Trim() ?? "";
        await _settingsService.SaveAsync(_settings);
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        // FolderPicker 需要 FileTypeFilter，随便加一个
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder? folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
    private async void BtnPickSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame is null)
        {
            await ShowSimpleDialogAsync("Please select a game first.");
            return;
        }

        var path = await PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            TxtSavePath.Text = path;
        }
    }

    private async void BtnSaveSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedGame == null)
            {
                await ShowErrorDialogAsync("Save Folder", "Please select a game first.");
                return;
            }

            var input = (TxtSavePath.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                await ShowErrorDialogAsync("Save Folder", "Please enter a save folder path.");
                return;
            }

            // 1) 用户输入包含 %TOKEN%：直接当作模板存
            string template = input.Contains('%')
                ? input
                : PathTemplateResolver.ToBestTemplate(input);

            // 2) 解析成真实路径并校验
            var resolved = PathTemplateResolver.Expand(template);
            if (string.IsNullOrWhiteSpace(resolved) || !Directory.Exists(resolved))
            {
                await ShowErrorDialogAsync(
                    "Save Folder",
                    $"Folder does not exist after resolving:\n{resolved}\n\nStored template:\n{template}"
                );
                return;
            }

            // 3) 保存模板 + 运行时路径（可选）
            _selectedGame.SavePathTemplate = template;
            _selectedGame.SavePath = resolved;

            _settings.Games = _games.ToList();
            await _settingsService.SaveAsync(_settings);

            TxtBackupStatus.Text = $"Saved. Template: {_selectedGame.SavePathTemplate}";
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Save Folder", ex.Message);
        }
    }

    private async void BtnBackupNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedGame == null)
            {
                TxtBackupStatus.Text = "No game selected.";
                await ShowErrorDialogAsync("Backup Failed", "Please select a game first.");
                return;
            }

            var backupRoot = (TxtBackupRoot.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(backupRoot))
            {
                TxtBackupStatus.Text = "Backup root folder is empty.";
                await ShowErrorDialogAsync("Backup Failed", "Please set Backup Root Folder first.");
                return;
            }

            TxtBackupStatus.Text = $"Backing up: {_selectedGame.Name}...";

            // ✅ 这里是真正备份：有变化才备份
            var (backedUp, dest, newFp) = await _backupService.BackupGameIfChangedAsync(_selectedGame, backupRoot);

            // ✅ 保存最新指纹，供下次判断“是否变化”
            _selectedGame.SaveFingerprint = newFp;

            // ✅ 只有真的备份了，才更新“最近备份时间”
            if (backedUp)
            {
                _selectedGame.LastBackupTime = DateTimeOffset.Now;

                _backupService.CleanupOldBackups(_selectedGame, backupRoot, _settings.KeepLastBackups);

                TxtBackupStatus.Text = $"Backup completed. Folder: {dest}";
                RefreshBackupList();
            }
            else
            {
                TxtBackupStatus.Text = "No changes detected. Backup skipped.";
            }

            // ✅ 最后把设置写回 config.json，保证下次打开仍然有效
            _settings.BackupRootDir = backupRoot;
            _settings.Games = _games.ToList();
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            TxtBackupStatus.Text = "Backup failed.";
            await ShowErrorDialogAsync("Backup Failed", ex.Message);
        }
    }

    private void RefreshBackupList()
    {
        if (_selectedGame is null)
            return;

        var backupRoot = (TxtBackupRoot.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(backupRoot))
        {
            BackupListView.ItemsSource = null;
            return;
        }

        var safeName = MakeSafeFolderName(_selectedGame.Name);
        var gameBackupDir = Path.Combine(backupRoot, safeName);

        if (!Directory.Exists(gameBackupDir))
        {
            BackupListView.ItemsSource = null;
            return;
        }

        var items = Directory.GetDirectories(gameBackupDir)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(di => di.CreationTime)
            .Take(20)
            .Select(di => di.Name)
            .ToList();

        BackupListView.ItemsSource = items;
    }

    private static string MakeSafeFolderName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }

    private string? GetSelectedBackupVersionPath()
    {
        if (_selectedGame is null) return null;

        var backupRoot = (TxtBackupRoot.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(backupRoot)) return null;

        if (BackupListView.SelectedItem is not string versionName) return null;

        var safeName = MakeSafeFolderName(_selectedGame.Name);
        return Path.Combine(backupRoot, safeName, versionName);
    }

    private string? GetLatestBackupVersionPath()
    {
        if (_selectedGame is null) return null;

        var backupRoot = (TxtBackupRoot.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(backupRoot)) return null;

        var safeName = MakeSafeFolderName(_selectedGame.Name);
        var gameBackupDir = Path.Combine(backupRoot, safeName);
        if (!Directory.Exists(gameBackupDir)) return null;

        var latest = Directory.GetDirectories(gameBackupDir)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(di => di.CreationTime)
            .FirstOrDefault();

        return latest?.FullName;
    }

    private void BackupListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BackupListView.SelectedItem is string v)
        {
            TxtRestoreHint.Text = $"Selected version: {v}";
        }
        else
        {
            TxtRestoreHint.Text = "";
        }
    }

    private void BtnOpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // ✅ 统一从 SettingsService 取路径
            var configDir = _settingsService.ConfigDir;
            var configPath = _settingsService.ConfigPath;

            // 确保目录存在（无论 MSIX 还是非 MSIX）
            Directory.CreateDirectory(configDir);

            // 如果 config.json 已存在，就在资源管理器中选中它
            var args = File.Exists(configPath)
                ? $"/select,\"{configPath}\""
                : $"\"{configDir}\"";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = args,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _ = ShowErrorDialogAsync(
                "Open Config Folder Failed",
                ex.Message
            );
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async void BtnRestoreSelected_Click(object sender, RoutedEventArgs e)
    {
        var versionPath = GetSelectedBackupVersionPath();
        await RestoreFromVersionAsync(versionPath, "selected");
    }

    private async void BtnRestoreLatest_Click(object sender, RoutedEventArgs e)
    {
        var versionPath = GetLatestBackupVersionPath();
        await RestoreFromVersionAsync(versionPath, "latest");
    }

    private async Task RestoreFromVersionAsync(string? versionPath, string label)
    {
        if (_selectedGame is null)
        {
            await ShowSimpleDialogAsync("Please select a game first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedGame.SavePath))
        {
            await ShowSimpleDialogAsync("Please set the save folder first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(versionPath) || !Directory.Exists(versionPath))
        {
            await ShowSimpleDialogAsync($"No {label} backup version found.");
            return;
        }

        var ok = await ConfirmAsync(
            "Restore Save Files",
            "This will overwrite your current save files.\n\nFor safety, the app will back up your current saves first.\n\nContinue?"
        );

        if (!ok) return;

        try
        {
            BtnRestoreSelected.IsEnabled = false;
            BtnRestoreLatest.IsEnabled = false;
            TxtBackupStatus.Text = "Creating safety backup...";

            // 1) 先备份当前存档作为安全回滚点
            var safetyDir = await _backupService.BackupGameAsync(_selectedGame, (TxtBackupRoot.Text ?? "").Trim());

            TxtBackupStatus.Text = "Restoring selected backup...";

            // 2) 还原
            await _backupService.RestoreGameAsync(_selectedGame, versionPath);

            TxtBackupStatus.Text = $"Restore completed. Safety backup created: {safetyDir}";
            RefreshBackupList();
        }
        catch (Exception ex)
        {
            TxtBackupStatus.Text = "";
            await ShowSimpleDialogAsync(ex.Message);
        }
        finally
        {
            BtnRestoreSelected.IsEnabled = true;
            BtnRestoreLatest.IsEnabled = true;
        }
    }

    private async void TglAutoBackup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        if (_settings is null) return;

        _settings.AutoBackupOnStartup = TglAutoBackup.IsOn;
        await _settingsService.SaveAsync(_settings);
    }

    private async Task RunAutoBackupIfEnabledAsync()
    {
        if (!_settings.AutoBackupOnStartup)
            return;

        var backupRoot = (_settings.BackupRootDir ?? "").Trim();
        if (string.IsNullOrWhiteSpace(backupRoot))
            return;

        // 统一用 _games 作为数据源，避免 _settings.Games 与 _games 不一致
        var candidates = _games
            .Where(g => !string.IsNullOrWhiteSpace(g.SavePath) && Directory.Exists(g.SavePath!))
            .ToList();

        if (candidates.Count == 0)
            return;

        TxtBackupStatus.Text = $"Auto backup started: {candidates.Count} games...";
        BtnBackupNow.IsEnabled = false;

        int backedUpCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        foreach (var g in candidates)
        {
            try
            {
                TxtBackupStatus.Text = $"Auto backing up: {g.Name}...";

                var (backedUp, _, newFp) = await _backupService.BackupGameIfChangedAsync(g, backupRoot);
                g.SaveFingerprint = newFp;

                if (backedUp)
                {
                    g.LastBackupTime = DateTimeOffset.Now;
                    _backupService.CleanupOldBackups(g, backupRoot, _settings.KeepLastBackups);
                    backedUpCount++;
                }
                else
                {
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                // 不用弹窗，避免启动时一直打扰；在状态栏给出原因即可
                TxtBackupStatus.Text = $"Auto backup failed: {g.Name}. {ex.Message}";
            }
        }

        // 保存（把 _games 写回 settings，再保存到 config.json）
        _settings.Games = _games.ToList();
        await _settingsService.SaveAsync(_settings);

        // 刷新列表显示（如果你已经用 ObservableCollection，通常不需要重绑，这里保留也没问题）
        GameListView.ItemsSource = null;
        GameListView.ItemsSource = _games;

        TxtBackupStatus.Text = failedCount == 0
            ? $"Auto backup completed: {backedUpCount} backed up, {skippedCount} skipped."
            : $"Auto backup completed: {backedUpCount} backed up, {skippedCount} skipped, {failedCount} failed.";

        BtnBackupNow.IsEnabled = true;

        RefreshBackupList();
    }
    private async Task ShowErrorDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }
    private void BtnOpenBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedGame == null)
            {
                _ = ShowErrorDialogAsync("Open Backup Folder", "Please select a game first.");
                return;
            }

            var backupRoot = (_settings.BackupRootDir ?? "").Trim();
            if (string.IsNullOrWhiteSpace(backupRoot))
            {
                _ = ShowErrorDialogAsync("Open Backup Folder", "Backup root folder is not set.");
                return;
            }

            var safeName = MakeSafeFolderName(_selectedGame.Name);
            var gameBackupDir = Path.Combine(backupRoot, safeName);

            if (!Directory.Exists(gameBackupDir))
            {
                _ = ShowErrorDialogAsync("Open Backup Folder", "No backups found for this game yet.");
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{gameBackupDir}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _ = ShowErrorDialogAsync("Open Backup Folder Failed", ex.Message);
        }
    }

}