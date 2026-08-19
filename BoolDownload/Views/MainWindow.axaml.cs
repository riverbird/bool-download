using System;
using Avalonia.Controls;
using BoolDownload.ViewModels;

namespace BoolDownload.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (Content is MainView nav && nav.CurrentPage is DownloadView view && view.DataContext is DownloadViewModel vm)
            vm.Save();
    }
}