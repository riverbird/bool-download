using System;
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
using BoolDownload.ViewModels;

namespace BoolDownload.Views;

public partial class DownloadView : ContentPage
{
    public DownloadView()
    {
        InitializeComponent();
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
            await ShowMessageDialog("检查更新", "获取更新信息失败: " + ex.Message);
            return;
        }

        if (updateInfo == null)
        {
            await ShowMessageDialog("检查更新", "更新信息解析失败。");
            return;
        }

        var downloadKey = GetPlatformDownloadKey();
        if (downloadKey == null)
        {
            await ShowMessageDialog("检查更新", "不支持的操作系统或架构。");
            return;
        }

        var platformInfo = updateInfo.GetPlatformInfo(downloadKey);
        if (platformInfo == null)
        {
            await ShowMessageDialog("检查更新", "未找到对应平台的更新信息。");
            return;
        }

        var localVersionCode = AppVersion.VersionCode;
        if (localVersionCode >= platformInfo.VersionCode)
        {
            await ShowMessageDialog("检查更新", "您现在使用的是最新版本!");
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
            Content = new TextBlock
            {
                Text = message,
                Margin = new Thickness(24, 24, 24, 0),
                TextWrapping = TextWrapping.Wrap
            }
        };

        var okButton = new Button { Content = "确定", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 24, 24), MinWidth = 80 };
        okButton.Click += (_, _) => dialog.Close();
        var panel = new DockPanel();
        DockPanel.SetDock(okButton, Dock.Bottom);
        panel.Children.Add(okButton);
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Margin = new Thickness(24, 24, 24, 0),
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
            Text = info.UpdatePrompt?.Title ?? "有新版本可用",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(24, 24, 24, 8),
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
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            Margin = new Thickness(24, 0, 24, 8)
        };

        var notesText = new TextBlock
        {
            Text = info.Notes ?? "",
            Margin = new Thickness(24, 0, 24, 8),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
        };

        var line2 = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            Margin = new Thickness(24, 0, 24, 12)
        };

        var upgradeButton = new Button
        {
            Content = "升级",
            MinWidth = 80,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24, 0, 8, 24)
        };

        var cancelButton = new Button
        {
            Content = "取消",
            MinWidth = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 24, 24)
        };

        var dialog = new Window
        {
            Title = "检查更新",
            Width = 500,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
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
