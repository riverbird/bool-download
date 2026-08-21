using BoolDownload.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace BoolDownload.ViewModels;

public partial class NewMagnetLinkViewModel : ViewModelBase
{
    /// <summary>保存位置下拉列表，本地 JSON 配置持久化。</summary>
    public ObservableCollection<string> Folders { get; } = new();

    [ObservableProperty] public partial string MagnetUrl { get; set; } = string.Empty;

    /// <summary>可选的任务名称，留空则在下载开始后从种子元数据自动获取。</summary>
    [ObservableProperty] public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty] public partial string? SelectedFolder { get; set; }

    /// <summary>最大连接数（对应 MonoTorrent 的连接数限制）。</summary>
    [ObservableProperty] public partial decimal MaxConnections { get; set; } = 50;

    public NewMagnetLinkViewModel()
    {
        foreach (var folder in LoadFolders())
            Folders.Add(folder);
        SelectedFolder = Folders.Count > 0 ? Folders[0] : null;
    }

    /// <summary>校验输入：磁力链接必须以 magnet:? 开头。</summary>
    public bool Validate()
    {
        return !string.IsNullOrWhiteSpace(MagnetUrl)
            && MagnetUrl.TrimStart().StartsWith("magnet:", StringComparison.OrdinalIgnoreCase);
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
