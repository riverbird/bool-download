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
using System.Diagnostics;
using System.IO;
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
    public DownloadEngine Engine { get; set; }
}

public partial class DownloadViewModel : ViewModelBase
{
    private readonly XunleiService _service = new();
    private readonly Dictionary<ulong, DownloadItem> _activeTasks = new();
    private readonly Dictionary<DownloadItem, NativeDownload> _nativeDownloads = new();
    private readonly Dictionary<DownloadItem, DownloaderDownload> _downloaderDownloads = new();
    private DispatcherTimer? _timer;

    public ObservableCollection<StatusItem> StatusItems { get; } = new()
    {
        new() { Name = "下载中", Icon = Icon.ArrowDownload, Matches = i => i.Status != "已完成" && i.Status != "已删除" },
        new() { Name = "已完成", Icon = Icon.CheckmarkCircle, Matches = i => i.Status == "已完成" },
        new() { Name = "回收站", Icon = Icon.Recycle, Matches = i => i.Status == "已删除" },
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
        // 不使用 ConfigureAwait(false)：确保恢复流程的后续操作回到 UI 线程，
        // 满足 Avalonia 的 UI 线程规则。
        _ = LoadSavedTasksAsync();
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
                    Engine = s.Engine,
                };
                item.PropertyChanged += OnDownloadItemChanged;
                Items.Add(item);
            }

            Refresh();

            var pending = Items.Where(i => i.Status is not "已完成" and not "已删除").ToList();
            if (pending.Count == 0) return;

            // 仅当存在迅雷引擎任务时才需要登录，原生引擎任务无需登录。
            var loggedIn = !pending.Any(i => i.Engine == DownloadEngine.Xunlei);
            if (!loggedIn)
                loggedIn = await _service.EnsureLoggedInAsync();

            foreach (var item in pending)
            {
                if (item.Engine == DownloadEngine.Native)
                {
                    ResumeNativeDownload(item);
                    continue;
                }

                if (item.Engine == DownloadEngine.Downloader)
                {
                    ResumeDownloaderDownload(item);
                    continue;
                }

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
            Engine = i.Engine,
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
            Engine = dialogVm.Engine,
        };
        item.PropertyChanged += OnDownloadItemChanged;
        Items.Add(item);
        Refresh();
        Save();

        // 原生下载引擎（HttpClient / Downloader）：直接分段下载，无需迅雷 SDK。
        if (dialogVm.Engine == DownloadEngine.Native)
        {
            StartNativeDownload(item, dialogVm.SelectedFolder, dialogVm.FileName, (int)dialogVm.MaxConnections);
            return;
        }

        if (dialogVm.Engine == DownloadEngine.Downloader)
        {
            StartDownloaderDownload(item, dialogVm.SelectedFolder, dialogVm.FileName, (int)dialogVm.MaxConnections);
            return;
        }

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
        if (SelectedItem is not { } item) return;

        if (item.Engine == DownloadEngine.Native)
        {
            if (_nativeDownloads.TryGetValue(item, out var download))
            {
                download.Start();
                item.Status = "下载中";
            }
            return;
        }

        if (item.Engine == DownloadEngine.Downloader)
        {
            if (_downloaderDownloads.TryGetValue(item, out var downloader))
            {
                if (downloader.State == NativeDownloadState.Paused)
                    downloader.Resume();
                else
                    downloader.Start();
                item.Status = "下载中";
            }
            return;
        }

        if (item.TaskId > 0)
            _service.StartTask(item.TaskId);
    }

    private bool CanStart()
    {
        if (SelectedItem is not { } item) return false;
        if (item.Engine == DownloadEngine.Native)
            return _nativeDownloads.TryGetValue(item, out var download) &&
                   download.State == NativeDownloadState.Paused;
        if (item.Engine == DownloadEngine.Downloader)
            return _downloaderDownloads.TryGetValue(item, out var downloader) &&
                   downloader.State == NativeDownloadState.Paused;
        return IsPaused(item);
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        if (SelectedItem is not { } item) return;

        if (item.Engine == DownloadEngine.Native)
        {
            if (_nativeDownloads.TryGetValue(item, out var download))
                download.Pause();
            return;
        }

        if (item.Engine == DownloadEngine.Downloader)
        {
            if (_downloaderDownloads.TryGetValue(item, out var downloader))
                downloader.Pause();
            return;
        }

        if (item.TaskId > 0)
            _service.StopTask(item.TaskId);
    }

    private bool CanPause()
    {
        if (SelectedItem is not { } item) return false;
        if (item.Engine == DownloadEngine.Native)
            return _nativeDownloads.TryGetValue(item, out var download) &&
                   download.State == NativeDownloadState.Downloading;
        if (item.Engine == DownloadEngine.Downloader)
            return _downloaderDownloads.TryGetValue(item, out var downloader) &&
                   downloader.State == NativeDownloadState.Downloading;
        return IsDownloading(item);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedItem is not { } item) return;

        var owner = GetMainWindow();
        if (owner is null) return;

        var confirm = new DeleteConfirmDialog();
        var ok = await confirm.ShowDialog<bool>(owner);
        if (!ok) return;

        if (item.Engine == DownloadEngine.Native)
        {
            if (_nativeDownloads.TryGetValue(item, out var download))
            {
                _nativeDownloads.Remove(item);
                await download.DeleteAsync(deleteFile: true);
                download.Dispose();
            }
            else
            {
                // 任务已完成后对象被释放，直接清理残留文件。
                DeleteNativeFiles(item, deleteFinal: true);
            }

            item.PropertyChanged -= OnDownloadItemChanged;
            Items.Remove(item);
            Refresh();
            RefreshCommandStates();
            Save();
            return;
        }

        if (item.Engine == DownloadEngine.Downloader)
        {
            if (_downloaderDownloads.TryGetValue(item, out var downloader))
            {
                _downloaderDownloads.Remove(item);
                await downloader.DeleteAsync(deleteFile: true);
                downloader.Dispose();
            }
            else
            {
                // 任务已完成后对象被释放，直接清理残留文件。
                DeleteDownloaderFiles(item, deleteFinal: true);
            }

            item.PropertyChanged -= OnDownloadItemChanged;
            Items.Remove(item);
            Refresh();
            RefreshCommandStates();
            Save();
            return;
        }

        _service.DeleteTask(item.TaskId, deleteFile: true);
        _activeTasks.Remove(item.TaskId);
        item.PropertyChanged -= OnDownloadItemChanged;
        Items.Remove(item);
        Refresh();
        RefreshCommandStates();
        Save();
    }

    /// <summary>
    /// 右键菜单“删除”：下载中/已完成的任务移入回收站（状态标记为“已删除”），
    /// 已处于回收站的任务则直接删除条目。
    /// </summary>
    [RelayCommand]
    private void MoveToTrash()
    {
        if (SelectedItem is not { } item) return;

        // 已处于回收站（状态为“已删除”）：直接删除该条目。
        if (item.Status == "已删除")
        {
            if (item.Engine == DownloadEngine.Native)
            {
                if (_nativeDownloads.TryGetValue(item, out var download))
                {
                    _nativeDownloads.Remove(item);
                    download.Pause();
                    download.Dispose();
                }
            }
            else if (item.Engine == DownloadEngine.Downloader)
            {
                if (_downloaderDownloads.TryGetValue(item, out var downloader))
                {
                    _downloaderDownloads.Remove(item);
                    downloader.Pause();
                    downloader.Dispose();
                }
            }
            else
            {
                _activeTasks.Remove(item.TaskId);
            }

            item.PropertyChanged -= OnDownloadItemChanged;
            Items.Remove(item);
            Refresh();
            RefreshCommandStates();
            Save();
            return;
        }

        // 正在下载的任务先停止下载（原生引擎暂停并保留分片，迅雷引擎停止任务）。
        if (IsActiveDownload(item))
        {
            if (item.Engine == DownloadEngine.Native)
            {
                if (_nativeDownloads.TryGetValue(item, out var download))
                {
                    download.Pause();
                    _nativeDownloads.Remove(item);
                }
            }
            else if (item.Engine == DownloadEngine.Downloader)
            {
                if (_downloaderDownloads.TryGetValue(item, out var downloader))
                {
                    downloader.Pause();
                    _downloaderDownloads.Remove(item);
                }
            }
            else if (item.TaskId > 0)
            {
                _service.StopTask(item.TaskId);
                // 停止轮询，避免轮询结果覆盖“已删除”状态。
                _activeTasks.Remove(item.TaskId);
            }
        }

        // 移入回收站。
        item.Status = "已删除";
        Refresh();
        RefreshCommandStates();
        Save();
    }

    /// <summary>使用原生下载引擎创建并开始一个下载任务。</summary>
    private void StartNativeDownload(DownloadItem item, string directory, string fileName, int segmentCount)
    {
        try
        {
            var download = new NativeDownload(item.Url, directory, fileName, segmentCount);
            _nativeDownloads[item] = download;
            download.ProgressChanged += (_, p) => OnNativeProgress(download, item, p);
            download.Completed += (_, _) => OnNativeCompleted(download, item);
            item.Status = "下载中";
            download.Start();
            Save();
        }
        catch (Exception)
        {
            item.Status = "下载失败";
        }
    }

    /// <summary>应用启动时恢复原生下载任务（断点续传）。</summary>
    private void ResumeNativeDownload(DownloadItem item)
    {
        try
        {
            var download = new NativeDownload(item.Url, item.SavePath, item.Name, Math.Max(1, item.MaxConnections));
            _nativeDownloads[item] = download;
            download.ProgressChanged += (_, p) => OnNativeProgress(download, item, p);
            download.Completed += (_, _) => OnNativeCompleted(download, item);
            item.Status = "等待";
            download.Start();
        }
        catch (Exception)
        {
            item.Status = "恢复失败";
        }
    }

    /// <summary>原生下载进度上报（后台线程触发，统一切换到 UI 线程更新界面）。</summary>
    private void OnNativeProgress(NativeDownload download, DownloadItem item, NativeDownloadProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!Items.Contains(item)) return;
            // 任务已被移入回收站：不再更新其进度。
            if (item.Status == "已删除") return;

            item.Size = FormatBytes((ulong)Math.Max(0, progress.TotalBytes));
            item.Done = FormatBytes((ulong)Math.Max(0, progress.DownloadedBytes));
            if (progress.TotalBytes > 0)
                item.Progress = (double)progress.DownloadedBytes / progress.TotalBytes * 100;
            if (item.Status is "创建中" or "等待")
                item.Status = "下载中";

            DownloadSpeed = FormatSpeed((ulong)Math.Max(0, progress.Speed));
            TrySaveProgress();
        });
    }

    /// <summary>原生下载结束（完成/暂停/失败）处理（后台线程触发，统一切换到 UI 线程更新界面）。</summary>
    private void OnNativeCompleted(NativeDownload download, DownloadItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!Items.Contains(item)) return;

            switch (download.State)
            {
                case NativeDownloadState.Completed:
                    item.Size = FormatBytes((ulong)Math.Max(0, download.TotalBytes));
                    item.Done = item.Size;
                    item.Progress = 100;
                    item.Status = "已完成";
                    _nativeDownloads.Remove(item);
                    download.Dispose();
                    break;
                case NativeDownloadState.Paused:
                    item.Status = "已暂停";
                    break;
                default:
                    item.Status = "下载失败";
                    break;
            }

            Refresh();
            RefreshCommandStates();
            Save();
        });
    }

    /// <summary>清理原生下载任务的残留分片与最终文件。</summary>
    private static void DeleteNativeFiles(DownloadItem item, bool deleteFinal)
    {
        try
        {
            if (Directory.Exists(item.SavePath))
            {
                foreach (var file in Directory.EnumerateFiles(item.SavePath, item.Name + ".part*"))
                    File.Delete(file);
            }

            if (deleteFinal)
            {
                var final = Path.Combine(item.SavePath, item.Name);
                if (File.Exists(final)) File.Delete(final);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    /// <summary>使用 Downloader 下载引擎创建并开始一个下载任务。</summary>
    private void StartDownloaderDownload(DownloadItem item, string directory, string fileName, int segmentCount)
    {
        try
        {
            var download = new DownloaderDownload(item.Url, directory, fileName, segmentCount);
            _downloaderDownloads[item] = download;
            download.ProgressChanged += (_, p) => OnDownloaderProgress(item, p);
            download.Completed += (_, _) => OnDownloaderCompleted(download, item);
            item.Status = "下载中";
            download.Start();
            Save();
        }
        catch (Exception)
        {
            item.Status = "下载失败";
        }
    }

    /// <summary>应用启动时恢复 Downloader 下载任务（断点续传）。</summary>
    private void ResumeDownloaderDownload(DownloadItem item)
    {
        try
        {
            var download = new DownloaderDownload(item.Url, item.SavePath, item.Name, Math.Max(1, item.MaxConnections));
            _downloaderDownloads[item] = download;
            download.ProgressChanged += (_, p) => OnDownloaderProgress(item, p);
            download.Completed += (_, _) => OnDownloaderCompleted(download, item);
            item.Status = "等待";
            download.Start();
        }
        catch (Exception)
        {
            item.Status = "恢复失败";
        }
    }

    /// <summary>Downloader 下载进度上报（后台线程触发，统一切换到 UI 线程更新界面）。</summary>
    private void OnDownloaderProgress(DownloadItem item, NativeDownloadProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!Items.Contains(item)) return;
            // 任务已被移入回收站：不再更新其进度。
            if (item.Status == "已删除") return;

            item.Size = FormatBytes((ulong)Math.Max(0, progress.TotalBytes));
            item.Done = FormatBytes((ulong)Math.Max(0, progress.DownloadedBytes));
            if (progress.TotalBytes > 0)
                item.Progress = (double)progress.DownloadedBytes / progress.TotalBytes * 100;
            if (item.Status is "创建中" or "等待")
                item.Status = "下载中";

            DownloadSpeed = FormatSpeed((ulong)Math.Max(0, progress.Speed));
            TrySaveProgress();
        });
    }

    /// <summary>Downloader 下载结束（完成/暂停/失败）处理（后台线程触发，统一切换到 UI 线程更新界面）。</summary>
    private void OnDownloaderCompleted(DownloaderDownload download, DownloadItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!Items.Contains(item)) return;

            // 任务已被移入回收站：不覆盖其状态，仅释放下载对象。
            if (item.Status == "已删除")
            {
                _downloaderDownloads.Remove(item);
                download.Dispose();
                return;
            }

            switch (download.State)
            {
                case NativeDownloadState.Completed:
                    item.Size = FormatBytes((ulong)Math.Max(0, download.TotalBytes));
                    item.Done = item.Size;
                    item.Progress = 100;
                    item.Status = "已完成";
                    _downloaderDownloads.Remove(item);
                    download.Dispose();
                    break;
                case NativeDownloadState.Paused:
                    item.Status = "已暂停";
                    break;
                default:
                    item.Status = "下载失败";
                    break;
            }

            // 所有任务均不在下载时，下载速度置 0。
            if (!Items.Any(IsActiveDownload))
                DownloadSpeed = "0 B/s";

            Refresh();
            RefreshCommandStates();
            Save();
        });
    }

    /// <summary>清理 Downloader 下载任务的残留包文件与最终文件。</summary>
    private static void DeleteDownloaderFiles(DownloadItem item, bool deleteFinal)
    {
        try
        {
            var final = Path.Combine(item.SavePath, item.Name);
            var package = final + ".download";
            if (File.Exists(package)) File.Delete(package);

            if (deleteFinal && File.Exists(final)) File.Delete(final);
        }
        catch
        {
            // Ignore cleanup failures.
        }
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

    /// <summary>右键菜单“打开文件”：任务已完成时用系统默认方式打开下载好的文件。</summary>
    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private void OpenFile()
    {
        if (SelectedItem is not { } item) return;
        if (item.Status != "已完成") return;

        var filePath = Path.Combine(item.SavePath, item.Name);
        if (!File.Exists(filePath)) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open") { UseShellExecute = true, ArgumentList = { filePath } });
            }
            else
            {
                Process.Start(new ProcessStartInfo("xdg-open") { UseShellExecute = true, ArgumentList = { filePath } });
            }
        }
        catch (Exception)
        {
            // 打开文件失败时忽略。
        }
    }

    private bool CanOpenFile()
    {
        if (SelectedItem is not { } item) return false;
        return item.Status == "已完成";
    }

    [RelayCommand]
    private void OpenSaveFolder()
    {
        if (SelectedItem is not { } item) return;

        var directory = item.SavePath;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

        var filePath = Path.Combine(directory, item.Name);
        var selectFile = File.Exists(filePath) || Directory.Exists(filePath);
        if (!selectFile)
        {
            var matched = Directory.EnumerateFiles(directory)
                .FirstOrDefault(f => Path.GetFileName(f).StartsWith(item.Name, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                filePath = matched;
                selectFile = true;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
                Arguments = selectFile ? $"/select,\"{filePath}\"" : $"\"{directory}\"",
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                UseShellExecute = true,
                Arguments = selectFile ? $"-R \"{filePath}\"" : $"\"{directory}\"",
            });
        }
        else
        {
            var fileUri = (selectFile ? new Uri(filePath) : new Uri(directory)).AbsoluteUri;
            var directoryUri = new Uri(directory).AbsoluteUri;
            var psi = new ProcessStartInfo("bash") { UseShellExecute = false };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(
                $"(dbus-send --session --dest=org.freedesktop.FileManager1 --type=method_call " +
                $"/org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems " +
                $"array:string:\"{fileUri}\" string:\"\" >/dev/null 2>&1) || " +
                $"xdg-open \"{directoryUri}\" >/dev/null 2>&1 &");
            Process.Start(psi);
        }
    }

    private static bool IsDownloading(DownloadItem item) =>
        item.Status is "等待" or "下载中";

    /// <summary>判断任务是否处于活跃下载状态（用于速度归零与移入回收站前的停止判断）。</summary>
    private static bool IsActiveDownload(DownloadItem item) =>
        item.Status is "创建中" or "等待" or "启动中" or "下载中";

    private static bool IsPaused(DownloadItem item) =>
        item.Status is "暂停中" or "已暂停";

    private void RefreshCommandStates()
    {
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        OpenFileCommand.NotifyCanExecuteChanged();
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
        var anyXunleiActive = false;

        foreach (var (taskId, item) in _activeTasks)
        {
            var state = _service.GetTaskState(taskId);
            if (state is null) continue;

            ApplyTaskState(item, state.Value);
            if (IsDownloading(item))
            {
                totalDownloadSpeed += state.Value.Speed;
                anyXunleiActive = true;
            }
        }

        if (anyXunleiActive)
        {
            DownloadSpeed = FormatSpeed(totalDownloadSpeed);
        }
        else if (!Items.Any(IsActiveDownload))
        {
            // 所有任务均已完成（或暂停/失败），下载速度置 0。
            DownloadSpeed = "0 B/s";
        }

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