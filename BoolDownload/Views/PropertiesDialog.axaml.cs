using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BoolDownload.Views;

public partial class PropertiesDialog : Window
{
    public PropertiesDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}