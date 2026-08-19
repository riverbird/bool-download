using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using BoolDownload.ViewModels;

namespace BoolDownload.Views;

public partial class DownloadView : ContentPage
{
    public DownloadView()
    {
        InitializeComponent();
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
}