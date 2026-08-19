using Avalonia;
using Avalonia.Controls;
using BoolDownload.ViewModels;

namespace BoolDownload.Views;

public partial class MainView : NavigationPage
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (CurrentPage == null)
        {
            await PushAsync(new DownloadView()
            {
                DataContext = new DownloadViewModel()
            });
        }
    }
}