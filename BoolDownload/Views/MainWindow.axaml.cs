using System;
using Avalonia.Controls;
using BoolDownload.Services;
using BoolDownload.ViewModels;

namespace BoolDownload.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MicaEffect.Attach(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (RootView.CurrentPage is DownloadView view && view.DataContext is DownloadViewModel vm)
            vm.Save();
    }
}
