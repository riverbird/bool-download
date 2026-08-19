using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BoolDownload.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is not null ? version.ToString(3) : "1.0.0";

        VersionText.Text = $"版本 {versionText}";
        SoftwareVersionText.Text = versionText;
        CopyrightText.Text = $"Copyright © {DateTime.Now.Year}";
        LicenseText.Text = "GPL-3.0 license";
        EngineText.Text = "Xunlei Open Download SDK";

        OsText.Text = $"{RuntimeInformation.OSDescription}";
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;
        ArchitectureText.Text = $"{RuntimeInformation.ProcessArchitecture} ({RuntimeInformation.OSArchitecture})";
        BaseDirectoryText.Text = AppContext.BaseDirectory;

        ReleaseNoteText.Text =
            "BoolDownload 1.0.0\n" +
            "================\n" +
            "\n" +
            "基于 Avalonia 12 构建的跨平台下载管理工具,集成迅雷开放下载引擎(Xunlei Open Download SDK),\n" +
            "支持多渠道断点续传下载。\n" +
            "\n" +
            "\n" +
            "2026-08-19 v1.0.0\n" +
            "-----------------\n" +
            "- 完成基础界面:顶部菜单栏、状态分类侧栏、下载列表(DataGrid)及工具栏。\n" +
            "- 新增\"新建下载\"对话框,支持设置下载链接、文件名、保存位置与最大连接数。\n" +
            "- 集成迅雷开放下载引擎,支持任务创建、启动、暂停、删除与进度轮询。\n" +
            "- 左侧分类列表支持按状态过滤下载项。\n" +
            "- 新增\"文件 > 退出\"退出应用程序。\n" +
            "- 新增\"帮助 > 关于\"关于对话框(软件信息 / 系统运行环境 / Release Note)。\n" +
            "- 顶部标题栏左侧显示应用程序 Logo。";
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}