using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BoolDownload.ViewModels;

/// <summary>单个加速节点：展示标题、生成的加速链接，并提供复制与打开操作。</summary>
public partial class AccelNode : ViewModelBase
{
    [ObservableProperty] public partial string Title { get; set; } = string.Empty;

    [ObservableProperty] public partial string Link { get; set; } = string.Empty;

    [RelayCommand]
    private async Task CopyLink()
    {
        if (string.IsNullOrEmpty(Link)) return;

        var window = GetMainWindow();
        if (window?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(Link);
    }

    private static Window? GetMainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: not null } desktop
            ? desktop.MainWindow
            : null;
}

/// <summary>文件加速对话框的视图模型：按用户选择的加速类型与输入链接生成 4 种加速链接。</summary>
public partial class AcceleratorViewModel : ViewModelBase
{
    public ObservableCollection<string> AccelTypes { get; } = new()
    {
        "Github加速",
        "SourceForge加速",
    };

    [ObservableProperty] public partial string SelectedType { get; set; } = "Github加速";

    [ObservableProperty] public partial string InputUrl { get; set; } = string.Empty;

    public ObservableCollection<AccelNode> Nodes { get; } = new();

    // 4 个加速节点（标题取自 gh-proxy.com 的节点说明，域名取自其转换逻辑）。
    private static readonly (string Title, string Domain)[] NodeDefs =
    {
        ("cloudflare (v4) 主站加速，全球高速分发！国内优选和IPv6网络支持请使用优选服务器。", "gh-proxy.org"),
        ("Cloudflare (v4)推荐 优选加速服务器，仅支持IPv4 网络智能解析。", "v4.gh-proxy.org"),
        ("Cloudflare (v4/v6) 优选加速服务器，支持 IPv6/IPv4 网络智能解析。", "v6.gh-proxy.org"),
        ("Fastly (v4) Fastly CDN 节点加速。", "cdn.gh-proxy.org"),
    };

    public AcceleratorViewModel()
    {
        foreach (var def in NodeDefs)
            Nodes.Add(new AccelNode { Title = def.Title });
    }

    [RelayCommand]
    private void Convert()
    {
        var input = (InputUrl ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(input)) return;

        // 去除前后引号与 git/wget/curl 前缀（兼容用户直接粘贴的命令）。
        input = input.Trim('"', '\'', '`');
        input = Regex.Replace(input, @"^(git\s+clone\s+|wget\s+|curl\s+-O\s+)", string.Empty, RegexOptions.IgnoreCase);

        // 补全协议前缀。
        if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            input = "https://" + input;

        if (!Uri.TryCreate(input, UriKind.Absolute, out _))
            return;

        var isSourceForge = SelectedType == "SourceForge加速";
        for (var i = 0; i < Nodes.Count; i++)
        {
            var domain = NodeDefs[i].Domain;
            // Github 加速：https://{domain}/{原始链接}
            // SourceForge 加速：https://{domain}/sourceforge/{原始链接}
            Nodes[i].Link = isSourceForge
                ? $"https://{domain}/sourceforge/{input}"
                : $"https://{domain}/{input}";
        }
    }
}
