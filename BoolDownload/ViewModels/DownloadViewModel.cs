using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace BoolDownload.ViewModels;

public partial class StatusItem : ViewModelBase
{
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial int Count { get; set; }
}

public partial class DownloadItem : ViewModelBase
{
    [ObservableProperty] public partial string Status { get; set; }
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Done { get; set; }
    [ObservableProperty] public partial string Size { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial DateTime AddedTime { get; set; }
}

public partial class DownloadViewModel : ViewModelBase
{
    public ObservableCollection<StatusItem> StatusItems { get; } = new()
    {
        new() { Name = "下载中", Count = 2 },
        new() { Name = "已完成", Count = 5 },
        new() { Name = "回收站", Count = 1 },
    };

    public ObservableCollection<DownloadItem> Items { get; } = new()
    {
        new() { Status = "下载中", Name = "Ubuntu 24.04.iso", Done = "1.2 GB", Size = "4.8 GB", Progress = 25, AddedTime = new DateTime(2026, 8, 19, 10, 30, 0) },
        new() { Status = "下载中", Name = "Visual Studio Installer", Done = "300 MB", Size = "1.1 GB", Progress = 28, AddedTime = new DateTime(2026, 8, 19, 10, 40, 0) },
        new() { Status = "已完成", Name = "Avalonia 12.1.1 Docs.zip", Done = "18 MB", Size = "18 MB", Progress = 100, AddedTime = new DateTime(2026, 8, 18, 16, 0, 0) },
        new() { Status = "已完成", Name = "Git for Windows", Done = "65 MB", Size = "65 MB", Progress = 100, AddedTime = new DateTime(2026, 8, 17, 9, 15, 0) },
        new() { Status = "已完成", Name = "JetBrains Rider 2026.2", Done = "1.8 GB", Size = "1.8 GB", Progress = 100, AddedTime = new DateTime(2026, 8, 15, 14, 20, 0) },
        new() { Status = "已完成", Name = "Python 3.13 Installer", Done = "28 MB", Size = "28 MB", Progress = 100, AddedTime = new DateTime(2026, 8, 12, 11, 5, 0) },
    };

    [ObservableProperty] public partial StatusItem? SelectedStatus { get; set; }

    [ObservableProperty] public partial DownloadItem? SelectedItem { get; set; }

    public DownloadViewModel()
    {
        SelectedStatus = StatusItems[0];
    }

    [RelayCommand]
    private void NewDownload()
    {
    }

    [RelayCommand]
    private void Start()
    {
    }

    [RelayCommand]
    private void Pause()
    {
    }

    [RelayCommand]
    private void Delete()
    {
    }

    [RelayCommand]
    private void Properties()
    {
    }
}