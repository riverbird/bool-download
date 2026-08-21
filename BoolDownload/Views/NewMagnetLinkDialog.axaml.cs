using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BoolDownload.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BoolDownload.Views;

public partial class NewMagnetLinkDialog : Window
{
    public NewMagnetLinkDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NewMagnetLinkViewModel vm)
        {
            Close(false);
            return;
        }

        if (!vm.Validate())
            return;

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnBrowseFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NewMagnetLinkViewModel vm)
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
}
