using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Downloader;

namespace BoolDownload.Services;

/// <summary>
/// 基于 Downloader 包（https://www.nuget.org/packages/Downloader）实现的下载引擎任务。
/// 通过 ChunkCount 实现分段并行下载（按“最大连接数”分段；服务端支持 Range 时并行分块，
/// 不支持时由库内部自动降级为单连接下载，与 HttpClient 引擎行为一致），
/// 通过 EnableAutoResumeDownload（.download 包文件）实现断点续传，
/// 支持暂停 / 恢复。全程不接触 UI 线程，进度通过事件上报，由调用方负责切换到 UI 线程。
/// </summary>
public sealed class DownloaderDownload : IDisposable
{
    private readonly object _sync = new();
    private readonly IDownload _download;
    private readonly string _filePath;
    private readonly string _packagePath;
    private Task? _runTask;
    private long _lastReportUtc;
    private bool _deleting;
    private bool _disposed;

    /// <summary>当前下载状态。</summary>
    public NativeDownloadState State { get; private set; } = NativeDownloadState.Pending;

    /// <summary>错误信息（失败时）。</summary>
    public string? Error { get; private set; }

    /// <summary>文件总大小（未知时为 -1）。</summary>
    public long TotalBytes { get; private set; } = -1;

    /// <summary>已下载字节数。</summary>
    public long DownloadedBytes { get; private set; }

    /// <summary>进度事件（后台线程触发）。</summary>
    public event EventHandler<NativeDownloadProgress>? ProgressChanged;

    /// <summary>结束事件：完成 / 暂停 / 失败（后台线程触发）。</summary>
    public event EventHandler? Completed;

    public DownloaderDownload(string url, string directory, string fileName, int segmentCount)
    {
        _filePath = Path.Combine(directory, fileName);
        _packagePath = _filePath + ".download";

        var config = new DownloadConfiguration
        {
            ChunkCount = Math.Clamp(segmentCount, 1, 128),
            ParallelDownload = true,
            // 注意：不能开启 RangeDownload = true。
            // Downloader 库在 RangeDownload=true 且服务端探测（Range: bytes=0-0）未返回
            // Content-Range 头（例如服务端忽略 Range、响应带 Content-Encoding、或返回
            // Accept-Ranges: none）时，会直接抛出 NotSupportedException 导致每次下载都失败。
            // 关闭后：服务端支持 Range 时仍会按 ChunkCount 分块并行下载；
            // 不支持 Range 时库内部自动降级为单连接下载（与 HttpClient 引擎行为一致）。
            RangeDownload = false,
            EnableAutoResumeDownload = true,
            MaxTryAgainOnFailure = 3,
            BufferBlockSize = 81920, // 80 KB
            // 默认 BlockTimeout 仅 5 秒，慢速/不稳定网络下单次读流超过 5s 就会
            // TaskCanceledException，重试 3 次后下载失败；放宽到 60 秒更稳健。
            BlockTimeout = 60000,
            CheckDiskSizeBeforeDownload = false,
        };

        _download = new DownloadBuilder()
            .WithUrl(url)
            .WithDirectory(directory)
            .WithFileName(fileName)
            .WithConfiguration(config)
            .Build();

        _download.DownloadProgressChanged += OnProgressChanged;
        _download.DownloadFileCompleted += OnFileCompleted;
    }

    /// <summary>开始（或重新开始）下载。</summary>
    public void Start()
    {
        lock (_sync)
        {
            if (State == NativeDownloadState.Downloading) return;
            State = NativeDownloadState.Downloading;
            _runTask = Task.Run(RunAsync);
        }
    }

    /// <summary>暂停下载（保留 .download 包文件，支持后续恢复）。</summary>
    public void Pause()
    {
        var shouldReport = false;
        lock (_sync)
        {
            if (State is NativeDownloadState.Downloading or NativeDownloadState.Pending)
            {
                State = NativeDownloadState.Paused;
                shouldReport = true;
            }
        }

        try { _download.Pause(); } catch { }

        // Downloader 暂停时不会触发 DownloadFileCompleted，这里主动上报，
        // 由调用方（ViewModel）将任务状态更新为“已暂停”。
        if (shouldReport)
            Completed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>从暂停状态恢复下载。</summary>
    public void Resume()
    {
        lock (_sync)
        {
            if (State != NativeDownloadState.Paused) return;
            State = NativeDownloadState.Downloading;
        }

        try { _download.Resume(); } catch { }
    }

    /// <summary>取消下载并删除相关文件。</summary>
    public async Task DeleteAsync(bool deleteFile)
    {
        _deleting = true;

        // Stop() 内部会同步等待取消完成，放到线程池执行避免阻塞 UI 线程。
        try { await Task.Run(() => _download.Stop()); } catch { }

        var task = _runTask;
        if (task is not null)
        {
            try { await task; } catch { }
        }

        try { if (File.Exists(_packagePath)) File.Delete(_packagePath); } catch { }

        if (deleteFile)
        {
            try { if (File.Exists(_filePath)) File.Delete(_filePath); } catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_download is IDisposable disposable)
                disposable.Dispose();
        }
        catch
        {
            // Ignore dispose failures.
        }
    }

    private async Task RunAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // DownloadBuilder 流程下 StartAsync() 直接写文件，返回的是 Stream.Null，
            // 无需（也不应）消费返回的流。
            await _download.StartAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            lock (_sync)
            {
                if (State == NativeDownloadState.Downloading)
                    State = NativeDownloadState.Failed;
            }
        }
    }

    private void OnProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        TotalBytes = e.TotalBytesToReceive;
        DownloadedBytes = e.ReceivedBytesSize;

        // 进度节流（约 200ms），避免高频刷新 UI。
        var now = DateTime.UtcNow.Ticks;
        if (now - _lastReportUtc < TimeSpan.FromMilliseconds(200).Ticks) return;
        _lastReportUtc = now;

        ProgressChanged?.Invoke(this, new NativeDownloadProgress(
            DownloadedBytes,
            TotalBytes,
            (long)e.BytesPerSecondSpeed));
    }

    private void OnFileCompleted(object? sender, AsyncCompletedEventArgs e)
    {
        // 删除过程中由 DeleteAsync 主动取消，无需再次上报结束。
        if (_deleting) return;

        lock (_sync)
        {
            if (e.Cancelled)
            {
                // 用户取消：保留 .download 包文件，可续传。
                State = NativeDownloadState.Paused;
            }
            else if (e.Error is not null)
            {
                Error = e.Error.Message;
                State = NativeDownloadState.Failed;
            }
            else
            {
                State = NativeDownloadState.Completed;
            }
        }

        Completed?.Invoke(this, EventArgs.Empty);
    }
}
