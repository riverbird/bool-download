using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BoolDownload.ViewModels;

namespace BoolDownload.Views;

public partial class HomeView : ContentPage
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void SettingButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.Navigation != null)
        {
            Navigation.PushAsync(new SettingsView()
            {
                DataContext = new SettingsViewModel()
            });
        }
    }
}