using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using BoolDownload.Services;
using BoolDownload.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Xunlei.XlDl;

namespace BoolDownload.ViewModels;

public partial class StatusItem : ViewModelBase
{
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial Icon Icon { get; set; }
    [ObservableProperty] public partial int Count { get; set; }
    public Func<DownloadItem, bool>? Matches { get; init; }
}

public partial class DownloadItem : ViewModelBase
{
    [ObservableProperty] public partial string Status { get; set; }
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Done { get; set; }
    [ObservableProperty] public partial string Size { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial DateTime AddedTime { get; set; }
    [ObservableProperty] public partial string Url { get; set; } = string.Empty;
    [ObservableProperty] public partial string SavePath { get; set; } = string.Empty;
    public ulong TaskId { get; set; }
    public int MaxConnections { get; set; }
}

public partial class DownloadViewModel : ViewModelBase
{
    private readonly XunleiService _service = new();
    private readonly Dictionary<ulong, DownloadItem> _activeTasks = new();
    private DispatcherTimer? _timer;

    public ObservableCollection<StatusItem> StatusItems { get; } = new()
    {
        new() { Name = "下载中", Icon = Icon.ArrowDownload, Matches = i => i.Status != "已完成" && i.Status != "回收站" },
        new() { Name = "已完成", Icon = Icon.CheckmarkCircle, Matches = i => i.Status == "已完成" },
        new() { Name = "回收站", Icon = Icon.Recycle, Matches = i => i.Status == "回收站" },
    };

    public ObservableCollection<DownloadItem> Items { get; } = new();

    public ObservableCollection<DownloadItem> FilteredItems { get; } = new();

    [ObservableProperty] public partial StatusItem? SelectedStatus { get; set; }

    [ObservableProperty] public partial DownloadItem? SelectedItem { get; set; }

    [ObservableProperty] public partial string UploadSpeed { get; set; } = FormatSpeed(0);

    [ObservableProperty] public partial string DownloadSpeed { get; set; } = FormatSpeed(0);

    private DateTime _lastSaveUtc = DateTime.UtcNow;

    public DownloadViewModel()
    {
        SelectedStatus = StatusItems[0];
        _ = LoadSavedTasksAsync().ConfigureAwait(false);
    }

    partial void OnSelectedStatusChanged(StatusItem? value) => Refresh();

    partial void OnSelectedItemChanged(DownloadItem? value) => RefreshCommandStates();

    private void OnDownloadItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadItem.Status))
        {
            Refresh();
            Save();
        }
    }

    private async Task LoadSavedTasksAsync()
    {
        try
        {
            var saved = DownloadTaskStore.Load();
            if (saved.Count == 0) return;

            foreach (var s in saved)
            {
                var item = new DownloadItem
                {
                    Status = s.Status,
                    Name = s.Name,
                    Done = s.Done,
                    Size = s.Size,
                    Progress = Math.Clamp(s.Progress, 0, 100),
                    AddedTime = s.AddedTime,
                    Url = s.Url,
                    SavePath = s.SavePath,
                    TaskId = s.TaskId,
                    MaxConnections = s.MaxConnections,
                };
                item.PropertyChanged += OnDownloadItemChanged;
                Items.Add(item);
            }

            Refresh();

            var pending = Items.Where(i => i.Status is not "已完成" and not "回收站").ToList();
            if (pending.Count == 0) return;

            var loggedIn = await _service.EnsureLoggedInAsync();
            foreach (var item in pending)
            {
                if (!loggedIn)
                {
                    item.Status = "登录失败";
                    continue;
                }

                var (result, taskId) = _service.CreateTask(item.Url, item.SavePath, item.Name);
                if (result != XLConstants.ErrorSuccess || taskId == 0)
                {
                    item.Status = "恢复失败";
                    continue;
                }

                item.TaskId = taskId;
                _activeTasks[taskId] = item;
                item.Status = _service.StartTask(taskId) == XLConstants.ErrorSuccess
                    ? "启动中"
                    : "启动失败";
                EnsureTimerRunning();
            }

            Save();
        }
        catch (Exception)
        {
            // Ignore loading failures.
        }
    }

    public void Save()
    {
        _lastSaveUtc = DateTime.UtcNow;
        DownloadTaskStore.Save(Items.Select(i => new SavedDownloadTask
        {
            Status = i.Status,
            Name = i.Name,
            Done = i.Done,
            Size = i.Size,
            Progress = i.Progress,
            AddedTime = i.AddedTime,
            Url = i.Url,
            SavePath = i.SavePath,
            TaskId = i.TaskId,
            MaxConnections = i.MaxConnections,
        }).ToList());
    }

    private void TrySaveProgress()
    {
        if ((DateTime.UtcNow - _lastSaveUtc).TotalSeconds < 2) return;
        Save();
    }

    public void Refresh()
    {
        UpdateCounts();

        var selected = SelectedStatus;
        FilteredItems.Clear();
        if (selected is null) return;

        foreach (var item in Items)
            if (selected.Matches?.Invoke(item) ?? true)
                FilteredItems.Add(item);
    }

    private void UpdateCounts()
    {
        foreach (var status in StatusItems)
        {
            var count = 0;
            if (status.Matches is not null)
            {
                foreach (var item in Items)
                    if (status.Matches(item)) count++;
            }
            status.Count = count;
        }
    }

    [RelayCommand]
    private async Task NewDownload()
    {
        var owner = GetMainWindow();
        if (owner is null) return;

        var dialogVm = new NewDownloadViewModel();
        var dialog = new NewDownloadDialog { DataContext = dialogVm };
        var ok = await dialog.ShowDialog<bool>(owner);
        if (!ok) return;

        if (string.IsNullOrWhiteSpace(dialogVm.SelectedFolder)) return;

        var item = new DownloadItem
        {
            Status = "创建中",
            Name = dialogVm.FileName,
            Url = dialogVm.Url,
            SavePath = dialogVm.SelectedFolder,
            Done = "0 B",
            Size = "",
            Progress = 0,
            AddedTime = DateTime.Now,
            MaxConnections = (int)dialogVm.MaxConnections,
        };
        item.PropertyChanged += OnDownloadItemChanged;
        Items.Add(item);
        Refresh();
        Save();

        try
        {
            var loggedIn = await _service.EnsureLoggedInAsync();
            if (!loggedIn)
            {
                item.Status = "登录失败";
                return;
            }

            var (result, taskId) = _service.CreateTask(
                dialogVm.Url,
                dialogVm.SelectedFolder,
                dialogVm.FileName);
            if (result != XLConstants.ErrorSuccess || taskId == 0)
            {
                item.Status = "创建失败";
                return;
            }

            item.TaskId = taskId;
            _activeTasks[taskId] = item;
            item.Status = "启动中";

            if (_service.StartTask(taskId) != XLConstants.ErrorSuccess)
                item.Status = "启动失败";

            EnsureTimerRunning();
        }
        catch (Exception)
        {
            item.Status = "下载失败";
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (SelectedItem is { TaskId: > 0 } item)
            _service.StartTask(item.TaskId);
    }

    private bool CanStart() => SelectedItem is { } item && IsPaused(item);

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        if (SelectedItem is { TaskId: > 0 } item)
            _service.StopTask(item.TaskId);
    }

    private bool CanPause() => SelectedItem is { } item && IsDownloading(item);

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedItem is not { TaskId: > 0 } item) return;

        var owner = GetMainWindow();
        if (owner is null) return;

        var confirm = new DeleteConfirmDialog();
        var ok = await confirm.ShowDialog<bool>(owner);
        if (!ok) return;

        _service.DeleteTask(item.TaskId, deleteFile: true);
        _activeTasks.Remove(item.TaskId);
        item.PropertyChanged -= OnDownloadItemChanged;
        Items.Remove(item);
        Refresh();
        RefreshCommandStates();
        Save();
    }

    [RelayCommand]
    private async Task Properties()
    {
        if (SelectedItem is not { } item) return;

        var owner = GetMainWindow();
        if (owner is null) return;

        var dialog = new PropertiesDialog { DataContext = new PropertiesViewModel(item) };
        await dialog.ShowDialog(owner);
    }

    private static bool IsDownloading(DownloadItem item) =>
        item.Status is "等待" or "下载中";

    private static bool IsPaused(DownloadItem item) =>
        item.Status is "暂停中" or "已暂停";

    private void RefreshCommandStates()
    {
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
    }

    private void EnsureTimerRunning()
    {
        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += OnPollTick;
        }
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        ulong totalDownloadSpeed = 0;

        foreach (var (taskId, item) in _activeTasks)
        {
            var state = _service.GetTaskState(taskId);
            if (state is null) continue;

            ApplyTaskState(item, state.Value);
            totalDownloadSpeed += state.Value.Speed;
        }

        DownloadSpeed = FormatSpeed(totalDownloadSpeed);
        RefreshCommandStates();
        TrySaveProgress();
    }

    private static void ApplyTaskState(DownloadItem item, TaskState state)
    {
        item.Size = FormatBytes(state.TotalSize);
        item.Done = FormatBytes(state.DownloadedSize);

        if (state.TotalSize > 0)
            item.Progress = (double)state.DownloadedSize / state.TotalSize * 100;

        item.Status = state.StateCode switch
        {
            XLConstants.TaskStatusStartWaiting or XLConstants.TaskStatusStartPending => "等待",
            XLConstants.TaskStatusStarted => "下载中",
            XLConstants.TaskStatusStopPending => "暂停中",
            XLConstants.TaskStatusStopped => "已暂停",
            XLConstants.TaskStatusSucceeded => "已完成",
            XLConstants.TaskStatusFailed => "失败",
            _ => item.Status,
        };

        if (item.Progress >= 100)
            item.Progress = 100;
    }

    private static string FormatBytes(ulong bytes)
    {
        if (bytes == 0) return "0 B";
        const ulong kb = 1024, mb = kb * 1024, gb = mb * 1024, tb = gb * 1024;
        return bytes switch
        {
            >= tb => $"{(double)bytes / tb:0.#} TB",
            >= gb => $"{(double)bytes / gb:0.#} GB",
            >= mb => $"{(double)bytes / mb:0.#} MB",
            >= kb => $"{(double)bytes / kb:0.#} KB",
            _ => $"{bytes} B",
        };
    }

    private static string FormatSpeed(ulong bytesPerSecond)
    {
        if (bytesPerSecond == 0) return "0 B/s";
        const ulong kb = 1024, mb = kb * 1024, gb = mb * 1024, tb = gb * 1024;
        return bytesPerSecond switch
        {
            >= tb => $"{(double)bytesPerSecond / tb:0.#} TB/s",
            >= gb => $"{(double)bytesPerSecond / gb:0.#} GB/s",
            >= mb => $"{(double)bytesPerSecond / mb:0.#} MB/s",
            >= kb => $"{(double)bytesPerSecond / kb:0.#} KB/s",
            _ => $"{bytesPerSecond} B/s",
        };
    }

    private static Window? GetMainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: not null } desktop
            ? desktop.MainWindow
            : null;
}