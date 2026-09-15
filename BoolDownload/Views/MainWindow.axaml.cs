using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;
using BoolDownload.ViewModels;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Windowing;

namespace BoolDownload.Views;

public partial class MainWindow : FAAppWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var tb = this.GetVisualDescendants().OfType<Panel>().FirstOrDefault(p => p.Name == "DefaultTitleBar");
        if (tb != null)
            tb.Background = new SolidColorBrush(Color.FromArgb(0x01, 0, 0, 0));

        if (OperatingSystem.IsWindowsVersionAtLeast(11, 0) && ActualThemeVariant != FluentAvaloniaTheme.HighContrastTheme)
        {
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica };
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (Content is MainView nav && nav.CurrentPage is DownloadView view && view.DataContext is DownloadViewModel vm)
            vm.Save();
    }
}