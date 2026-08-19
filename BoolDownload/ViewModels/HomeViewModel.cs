using CommunityToolkit.Mvvm.ComponentModel;

namespace BoolDownload.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}