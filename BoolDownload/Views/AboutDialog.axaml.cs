using Avalonia.Controls;
using Avalonia.Interactivity;
using Downloader;
using MonoTorrent.Client;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunlei.XlDl;

namespace BoolDownload.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is not null ? version.ToString(3) : "1.1.0";

        VersionText.Text = $"版本 {versionText}";
        SoftwareVersionText.Text = versionText;
        CopyrightText.Text = $"Copyright © {DateTime.Now.Year}";
        LicenseText.Text = "GPL-3.0 license";
        EngineText.Text = "Xunlei Open Download SDK";

        OsText.Text = $"{RuntimeInformation.OSDescription}";
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;
        ArchitectureText.Text = $"{RuntimeInformation.ProcessArchitecture} ({RuntimeInformation.OSArchitecture})";
        BaseDirectoryText.Text = AppContext.BaseDirectory;

        // 第三方库及版本（优先使用 AssemblyInformationalVersion 显示 NuGet 版本号）。
        AvaloniaVersionText.Text = GetPackageVersion(typeof(Avalonia.Controls.Window).Assembly);
        XunleiSdkVersionText.Text = GetPackageVersion(typeof(XLConstants).Assembly);
        DownloaderVersionText.Text = GetPackageVersion(typeof(DownloadBuilder).Assembly);
        MonoTorrentVersionText.Text = GetPackageVersion(typeof(ClientEngine).Assembly);

        ReleaseNoteText.Text =
            "BoolDownload 1.1.0\n" +
            "================\n" +
            "\n" +
            "基于 Avalonia 12 构建的跨平台下载管理工具,集成迅雷开放下载引擎(Xunlei Open Download SDK),\n" +
            "支持多渠道断点续传下载。\n" +
            "\n" +
            "\n" +
            "2026-08-21 v1.1.0\n" +
            "-----------------\n" +
            "- 新增：thunder://协议下载支持;\n" +
            "- 新增: 磁力链接下载:支持磁力链接任务的新建、暂停、恢复、删除与断点续传;\n" +
            "- 新增：下载引擎切换：原生引擎(HttpClient/Downloader)与迅雷引擎;\n" +
            "- 优化：下载目录可自定义;\n" +
            "- 优化：删除/删除条目及文件与回收站的逻辑;\n" +
            "- 优化：已完成的下载任务增加打开文件功能。\n" +
            "\n" +
            "\n" +
            "2026-08-19 v1.0.0\n" +
            "-----------------\n" +
            "- 完成基础界面:顶部菜单栏、状态分类侧栏、下载列表(DataGrid)及工具栏。\n" +
            "- 新增\"新建下载\"对话框,支持设置下载链接、文件名、保存位置与最大连接数。\n" +
            "- 集成迅雷开放下载引擎,支持任务创建、启动、暂停、删除与进度轮询。\n" +
            "- 左侧分类列表支持按状态过滤下载项。\n";
    }

    private static string GetPackageVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+');
            return plusIndex > 0 ? informational[..plusIndex] : informational;
        }

        var assemblyVersion = assembly.GetName().Version;
        return assemblyVersion?.ToString(3) ?? "?";
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
