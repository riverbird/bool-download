using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BoolDownload.Services;
using BoolDownload.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BoolDownload.Views;

public partial class NewDownloadDialog : Window
{
    public NewDownloadDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NewDownloadViewModel vm)
        {
            Close(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(vm.Url))
            return;

        // 将 thunder:// 迅雷专用链接解析还原为普通 http/https 直链后，
        // 再把解析后的 URL 传给下载任务。解析失败则保持对话框打开。
        var url = vm.Url.Trim();
        if (ThunderUrlParser.IsThunderUrl(url))
        {
            if (!ThunderUrlParser.TryParse(url, out var parsed))
                return;
            url = parsed;
            vm.Url = url;
        }

        if (string.IsNullOrWhiteSpace(vm.FileName))
        {
            var name = TryGetFileNameFromUrl(url);
            if (string.IsNullOrWhiteSpace(name)) return;
            vm.FileName = name;
        }

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnBrowseFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NewDownloadViewModel vm)
            return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        IReadOnlyList<IStorageFolder> folders;
        try
        {
            folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "选择保存位置",
                    AllowMultiple = false,
                });
        }
        catch (Exception)
        {
            return;
        }

        var picked = folders.FirstOrDefault();
        if (picked?.Path is not { } uri)
            return;

        var path = uri.IsFile ? uri.LocalPath : uri.OriginalString;
        if (string.IsNullOrWhiteSpace(path))
            return;

        // 将选中的目录加入下拉列表并设为当前选项（自动持久化到本地 JSON）。
        vm.AddFolder(path);
    }

    private static string? TryGetFileNameFromUrl(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var name = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(name))
                    return Uri.UnescapeDataString(name);
            }
        }
        catch (Exception)
        {
            return null;
        }
        return null;
    }
}
