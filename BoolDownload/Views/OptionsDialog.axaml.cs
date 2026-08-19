using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BoolDownload.Views;

public partial class OptionsDialog : Window
{
    public OptionsDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}