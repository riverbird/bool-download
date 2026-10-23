using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using BoolDownload.Models;
using BoolDownload.Services;
using BoolDownload.ViewModels;

namespace BoolDownload.Views;

public partial class DownloadView : ContentPage
{
    private DownloadViewModel? _viewModel;
    private DataGridColumn? _speedColumn;

    /// <summary>“速度”列在 DownloadGrid.Columns 中的下标。</summary>
    private const int SpeedColumnIndex = 5;

    public DownloadView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as DownloadViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateSpeedColumnVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadViewModel.SelectedStatus))
            UpdateSpeedColumnVisibility();
    }

    /// <summary>“速度”列仅在“下载中”列表显示，已完成列表不显示。</summary>
    private void UpdateSpeedColumnVisibility()
    {
        if (_speedColumn is null && DownloadGrid.Columns.Count > SpeedColumnIndex)
            _speedColumn = DownloadGrid.Columns[SpeedColumnIndex];

        if (_speedColumn is not null)
            _speedColumn.IsVisible = _viewModel?.SelectedStatus?.Name == "下载中";
    }

    private void OnCellsPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        if (e.PointerPressedEventArgs.GetCurrentPoint(this).Properties.IsRightButtonPressed
            && e.Row.DataContext is DownloadItem item
            && DataContext is DownloadViewModel vm)
        {
            vm.SelectedItem = item;
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DownloadViewModel vm) vm.Save();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void OnAbout(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            new AboutDialog().ShowDialog(owner);
    }

    private void OnOptions(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            new OptionsDialog().ShowDialog(owner);
    }

    private void OnAccelerator(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            var vm = DataContext as DownloadViewModel;
            new AcceleratorDialog(vm).ShowDialog(owner);
        }
    }

    private async void OnCheckUpdate(object? sender, RoutedEventArgs e)
    {
        UpdateInfo? updateInfo;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync("http://download.10qu.com.cn/bool-download/update.json");
            updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json);
        }
        catch (Exception ex)
        {
            await ShowMessageDialog(Localization.T("Loc_Menu_CheckUpdate"), Localization.T("Loc_Msg_CheckUpdateFailed") + ex.Message);
            return;
        }

        if (updateInfo == null)
        {
            await ShowMessageDialog(Localization.T("Loc_Menu_CheckUpdate"), Localization.T("Loc_Msg_UpdateParseFailed"));
            return;
        }

        var downloadKey = GetPlatformDownloadKey();
        if (downloadKey == null)
        {
            await ShowMessageDialog(Localization.T("Loc_Menu_CheckUpdate"), Localization.T("Loc_Msg_UnsupportedOs"));
            return;
        }

        var platformInfo = updateInfo.GetPlatformInfo(downloadKey);
        if (platformInfo == null)
        {
            await ShowMessageDialog(Localization.T("Loc_Menu_CheckUpdate"), Localization.T("Loc_Msg_NoPlatformUpdate"));
            return;
        }

        var localVersionCode = AppVersion.VersionCode;
        if (localVersionCode >= platformInfo.VersionCode)
        {
            await ShowMessageDialog(Localization.T("Loc_Menu_CheckUpdate"), Localization.T("Loc_Msg_UpdateLatest"));
            return;
        }

        await ShowUpdateDialog(updateInfo, downloadKey, platformInfo);
    }

    private static string? GetPlatformDownloadKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSArchitecture == Architecture.X64)
            return "windows_x64";

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return null;

        var arch = RuntimeInformation.OSArchitecture;
        var isDebian = IsDebianBased();
        var isRpm = IsRpmBased();

        if (isDebian && arch == Architecture.X64) return "deb_x64";
        if (isDebian && arch == Architecture.Arm64) return "deb_arm64";
        if (isRpm && arch == Architecture.X64) return "rpm_x64";
        if (isRpm && arch == Architecture.Arm64) return "rpm_arm64";

        return null;
    }

    private static bool IsDebianBased()
    {
        try
        {
            if (File.Exists("/etc/os-release"))
            {
                var lines = File.ReadAllLines("/etc/os-release");
                foreach (var line in lines)
                {
                    if (line.StartsWith("ID=", StringComparison.Ordinal))
                    {
                        var id = line.Substring(3).Trim('"', ' ');
                        return id is "debian" or "ubuntu" or "linuxmint" or "pop" or "kali" or "elementary" or "zorin" or "raspbian";
                    }
                }
            }
        }
        catch { }
        return false;
    }

    private static bool IsRpmBased()
    {
        try
        {
            if (File.Exists("/etc/os-release"))
            {
                var lines = File.ReadAllLines("/etc/os-release");
                foreach (var line in lines)
                {
                    if (line.StartsWith("ID=", StringComparison.Ordinal))
                    {
                        var id = line.Substring(3).Trim('"', ' ');
                        return id is "fedora" or "rhel" or "centos" or "rocky" or "alma" or "ol" or "opensuse" or "sles";
                    }
                }
            }
        }
        catch { }
        return false;
    }

    private IBrush? ThemeBrush(string key) => this.FindResource(key) as IBrush;

    private async Task ShowMessageDialog(string title, string message)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = ThemeBrush("DialogBackgroundBrush")
        };

        var okButton = new Button
        {
            Content = Localization.T("Loc_Btn_Ok"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 24, 20),
            MinWidth = 88,
            Height = 32
        };
        okButton.Classes.Add("accent");
        okButton.Click += (_, _) => dialog.Close();
        var panel = new DockPanel();
        DockPanel.SetDock(okButton, Dock.Bottom);
        panel.Children.Add(okButton);
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Margin = new Thickness(24, 20, 24, 0),
            TextWrapping = TextWrapping.Wrap
        });
        dialog.Content = panel;
        dialog.KeyDown += (_, k) => { if (k.Key == Key.Escape) dialog.Close(); };

        await dialog.ShowDialog(owner);
    }

    private async Task ShowUpdateDialog(UpdateInfo info, string downloadKey, PlatformInfo platformInfo)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var titleText = new TextBlock
        {
            Text = info.UpdatePrompt?.Title ?? Localization.T("Loc_Msg_NewVersionAvailable"),
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(24, 20, 24, 8),
            TextWrapping = TextWrapping.Wrap
        };

        var messageText = new TextBlock
        {
            Text = info.UpdatePrompt?.Message ?? "",
            Margin = new Thickness(24, 0, 24, 12),
            TextWrapping = TextWrapping.Wrap
        };

        var changelogText = string.Join(Environment.NewLine, info.Changelog?.Select(c => "• " + c) ?? []);
        var changelogBox = new TextBox
        {
            Text = changelogText,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 0, 24, 12),
            Height = 150
        };

        var line1 = new Border
        {
            Height = 1,
            Background = ThemeBrush("DividerBrush"),
            Margin = new Thickness(24, 0, 24, 8)
        };

        var notesText = new TextBlock
        {
            Text = info.Notes ?? "",
            Margin = new Thickness(24, 0, 24, 8),
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrush("MutedTextBrush")
        };

        var line2 = new Border
        {
            Height = 1,
            Background = ThemeBrush("DividerBrush"),
            Margin = new Thickness(24, 0, 24, 12)
        };

        var upgradeButton = new Button
        {
            Content = Localization.T("Loc_Btn_Upgrade"),
            MinWidth = 88,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24, 0, 8, 20)
        };
        upgradeButton.Classes.Add("accent");

        var cancelButton = new Button
        {
            Content = Localization.T("Loc_Btn_Cancel"),
            MinWidth = 88,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 24, 20)
        };
        cancelButton.Classes.Add("secondary");

        var dialog = new Window
        {
            Title = Localization.T("Loc_Menu_CheckUpdate"),
            Width = 500,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = ThemeBrush("DialogBackgroundBrush")
        };

        upgradeButton.Click += async (_, _) =>
        {
            var url = platformInfo.Download?.Url;
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
            dialog.Close();
        };

        cancelButton.Click += (_, _) => dialog.Close();
        dialog.KeyDown += (_, k) => { if (k.Key == Key.Escape) dialog.Close(); };

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(titleText);
        stack.Children.Add(messageText);
        stack.Children.Add(changelogBox);
        stack.Children.Add(line1);
        stack.Children.Add(notesText);
        stack.Children.Add(line2);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttonPanel.Children.Add(upgradeButton);
        buttonPanel.Children.Add(cancelButton);
        var buttonHost = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        buttonHost.Children.Add(buttonPanel);

        var root = new DockPanel();
        DockPanel.SetDock(buttonHost, Dock.Bottom);
        root.Children.Add(buttonHost);
        root.Children.Add(stack);

        dialog.Content = root;

        await dialog.ShowDialog(owner);
    }
}
