using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace BoolDownload.ViewModels;

public partial class NewDownloadViewModel : ViewModelBase
{
    public string[] Folders { get; } = BuildDefaultFolders();

    [ObservableProperty] public partial string Url { get; set; } = string.Empty;

    [ObservableProperty] public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty] public partial string? SelectedFolder { get; set; }

    [ObservableProperty] public partial decimal MaxConnections { get; set; } = 16;

    public NewDownloadViewModel()
    {
        SelectedFolder = Folders.Length > 0 ? Folders[0] : null;
    }

    private static string[] BuildDefaultFolders()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        var desktop = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Downloads");
        return new[]
        {
            downloads,
            desktop,
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        };
    }
}
