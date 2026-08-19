using Avalonia.Controls;
using Avalonia.Interactivity;
using BoolDownload.ViewModels;
using System;
using System.IO;

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

        if (string.IsNullOrWhiteSpace(vm.FileName))
        {
            var name = TryGetFileNameFromUrl(vm.Url);
            if (string.IsNullOrWhiteSpace(name)) return;
            vm.FileName = name;
        }

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

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
