using Avalonia.Controls;
using Avalonia.Interactivity;
using BoolDownload.ViewModels;

namespace BoolDownload.Views;

public partial class AcceleratorDialog : Window
{
    private readonly DownloadViewModel? _downloadViewModel;

    public AcceleratorDialog()
    {
        InitializeComponent();
        DataContext = new AcceleratorViewModel();
    }

    public AcceleratorDialog(DownloadViewModel? downloadViewModel) : this()
    {
        _downloadViewModel = downloadViewModel;
    }

    private async void OnDownloadNow(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AccelNode node })
            return;
        if (string.IsNullOrEmpty(node.Link))
            return;

        var dialogVm = new NewDownloadViewModel { Url = node.Link };
        var dialog = new NewDownloadDialog { DataContext = dialogVm };
        var ok = await dialog.ShowDialog<bool>(this);
        if (ok && _downloadViewModel is not null)
            await _downloadViewModel.CreateDownloadAsync(dialogVm);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
