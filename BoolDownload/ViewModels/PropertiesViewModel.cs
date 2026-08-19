using CommunityToolkit.Mvvm.ComponentModel;

namespace BoolDownload.ViewModels;

public partial class PropertiesViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Url { get; set; }

    [ObservableProperty] public partial string FileName { get; set; }

    [ObservableProperty] public partial string SavePath { get; set; }

    [ObservableProperty] public partial decimal MaxConnections { get; set; }

    public PropertiesViewModel(DownloadItem item)
    {
        Url = item.Url;
        FileName = item.Name;
        SavePath = item.SavePath;
        MaxConnections = item.MaxConnections;
    }
}
