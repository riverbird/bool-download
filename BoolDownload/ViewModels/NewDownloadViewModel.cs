using BoolDownload.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace BoolDownload.ViewModels;

public partial class NewDownloadViewModel : ViewModelBase
{
    /// <summary>保存位置下拉列表，本地 JSON 配置持久化。</summary>
    public ObservableCollection<string> Folders { get; } = new();

    [ObservableProperty] public partial string Url { get; set; } = string.Empty;

    [ObservableProperty] public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty] public partial string? SelectedFolder { get; set; }

    [ObservableProperty] public partial decimal MaxConnections { get; set; } = 4;

    /// <summary>下载引擎下拉选项，默认为第 1 项“迅雷开放下载引擎”。</summary>
    public string[] EngineOptions { get; } =
    {
        "迅雷开放下载引擎",
        "原生下载引擎(HttpClient)",
        "原生下载引擎(Downloader)",
    };

    [ObservableProperty] public partial int SelectedEngineIndex { get; set; }

    /// <summary>当前选中的下载引擎。</summary>
    public DownloadEngine Engine =>
        SelectedEngineIndex switch
        {
            1 => DownloadEngine.Native,
            2 => DownloadEngine.Downloader,
            _ => DownloadEngine.Xunlei,
        };

    public NewDownloadViewModel()
    {
        foreach (var folder in LoadFolders())
            Folders.Add(folder);
        SelectedFolder = Folders.Count > 0 ? Folders[0] : null;
    }

    /// <summary>
    /// 将用户通过目录选择对话框选中的目录加入下拉列表，
    /// 并设为当前选项，同时持久化到本地 JSON 配置文件。
    /// </summary>
    public void AddFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return;

        if (!Folders.Contains(folder))
        {
            Folders.Add(folder);
            FolderStore.Save(Folders.ToList());
        }
        SelectedFolder = folder;
    }

    private static List<string> LoadFolders()
    {
        var list = new List<string>();
        foreach (var folder in FolderStore.Load())
        {
            if (!string.IsNullOrWhiteSpace(folder) && !list.Contains(folder))
                list.Add(folder);
        }
        foreach (var folder in BuildDefaultFolders())
        {
            if (!string.IsNullOrWhiteSpace(folder) && !list.Contains(folder))
                list.Add(folder);
        }
        return list;
    }

    private static string[] BuildDefaultFolders()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        var desktop = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Downloads");
        return new[]
        {
            downloads,
            desktop,
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        };
    }
}
