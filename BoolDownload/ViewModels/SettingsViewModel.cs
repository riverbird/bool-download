using CommunityToolkit.Mvvm.ComponentModel;

namespace BoolDownload.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Greeting { get; set; } = "This is Settings";
}