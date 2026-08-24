using BoolDownload.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace BoolDownload.ViewModels;

/// <summary>“BT 下载”对话框的视图模型：展示种子名称、包含的文件列表与保存位置。</summary>
public partial class TorrentInfoViewModel : ViewModelBase
{
    /// <summary>保存位置下拉列表，本地 JSON 配置持久化。</summary>
    public ObservableCollection<string> Folders { get; } = new();

    [ObservableProperty] public partial string TorrentName { get; set; } = string.Empty;

    [ObservableProperty] public partial string FilePath { get; set; } = string.Empty;

    [ObservableProperty] public partial string? SelectedFolder { get; set; }

    /// <summary>最大连接数（对应 MonoTorrent 的连接数限制）。</summary>
    [ObservableProperty] public partial decimal MaxConnections { get; set; } = 50;

    /// <summary>种子内所有文件（含相对路径与大小）。</summary>
    public ObservableCollection<TorrentFileEntry> Files { get; } = new();

    public TorrentInfoViewModel()
    {
        foreach (var folder in LoadFolders())
            Folders.Add(folder);
        SelectedFolder = Folders.Count > 0 ? Folders[0] : null;
    }

    /// <summary>校验：必须已选择保存位置。</summary>
    public bool Validate() => !string.IsNullOrWhiteSpace(SelectedFolder);

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

/// <summary>BT 种子内单个文件的信息。</summary>
public class TorrentFileEntry
{
    /// <summary>文件相对路径（含文件夹层级）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>文件大小（字节）。</summary>
    public long Length { get; set; }

    /// <summary>格式化后的大小文本。</summary>
    public string SizeText => FormatBytes((ulong)Math.Max(0, Length));

    private static string FormatBytes(ulong bytes)
    {
        if (bytes == 0) return "0 B";
        const ulong kb = 1024, mb = kb * 1024, gb = mb * 1024, tb = gb * 1024;
        return bytes switch
        {
            >= tb => $"{(double)bytes / tb:0.#} TB",
            >= gb => $"{(double)bytes / gb:0.#} GB",
            >= mb => $"{(double)bytes / mb:0.#} MB",
            >= kb => $"{(double)bytes / kb:0.#} KB",
            _ => $"{bytes} B",
        };
    }
}
