using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MonoTorrent;
using MonoTorrent.Client;

namespace BoolDownload.Services;

/// <summary>
/// 磁力链接下载引擎：基于 MonoTorrent 3.0.2 实现。
/// 通过 magnet 协议（magnet:?xt=urn:btih:...）下载资源，支持断点续传与多任务并发。
/// 全局共享一个 <see cref="ClientEngine"/>，每个下载任务对应一个 <see cref="TorrentManager"/>。
/// 断点续传依赖 MonoTorrent 的 metadata 缓存与 fast resume 自动保存/加载机制
/// （缓存目录持久化到应用本地数据目录，重启后自动恢复）。
/// </summary>
public sealed class MagnetLinkDownload : IDisposable
{
private readonly object _sync = new();
    private readonly string _magnetUrl;
    private readonly string _saveDirectory;
    private readonly string? _preferredName;
    private readonly int _maxConnections;
    private System.Timers.Timer _pollTimer;
    private TorrentManager? _manager;
    private Task? _runTask;
    private bool _completed;
    private bool _deleting;
    private bool _disposed;
    private readonly TaskCompletionSource<bool> _metadataReady = new();

    /// <summary>种子管理器（metadata就绪后有效）。</summary>
    public TorrentManager? Manager => _manager;

    /// <summary>当磁力元数据就绪时完成的任务。</summary>
    public Task MetadataReadyTask => _metadataReady.Task;

    /// <summary>当前任务状态。</summary>
    public NativeDownloadState State { get; private set; } = NativeDownloadState.Pending;

    /// <summary>错误信息（失败时）。</summary>
    public string? Error { get; private set; }

    /// <summary>种子名称（metadata 下载完成后更新为真实名称）。</summary>
    public string? Name { get; private set; }

    /// <summary>文件总大小（metadata 就绪前为 -1）。</summary>
    public long TotalBytes { get; private set; } = -1;

    /// <summary>已下载字节数。</summary>
    public long DownloadedBytes { get; private set; }

    /// <summary>当前下载速度（字节/秒）。</summary>
    public long Speed { get; private set; }

    /// <summary>任务内容所在目录（保存目录/种子名，metadata 就绪后有效）。</summary>
    public string? ContainingDirectory { get; private set; }

    /// <summary>进度事件（后台线程触发，需自行切换到 UI 线程）。</summary>
    public event EventHandler<NativeDownloadProgress>? ProgressChanged;

    /// <summary>结束事件：完成 / 暂停 / 失败（后台线程触发）。</summary>
    public event EventHandler? Completed;

    public MagnetLinkDownload(string magnetUrl, string saveDirectory, string? preferredName = null, int maxConnections = 50)
    {
        _magnetUrl = magnetUrl;
        _saveDirectory = saveDirectory;
        _preferredName = preferredName;
        _maxConnections = Math.Clamp(maxConnections, 1, 300);

        _pollTimer = new System.Timers.Timer(500)
        {
            AutoReset = true,
        };
        _pollTimer.Elapsed += OnPollTick;
    }

    /// <summary>开始下载。</summary>
    public void Start()
    {
        lock (_sync)
        {
            if (State == NativeDownloadState.Downloading) return;
            State = NativeDownloadState.Downloading;
            _runTask = Task.Run(RunAsync);
        }
    }

    /// <summary>暂停下载（保留已下载数据，支持后续恢复）。</summary>
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

        if (_manager is { } manager)
        {
            try { manager.PauseAsync().GetAwaiter().GetResult(); } catch { /* 忽略 */ }
        }

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

        if (_manager is { } manager)
        {
            try { manager.StartAsync().GetAwaiter().GetResult(); } catch { /* 忽略 */ }
        }
    }

    /// <summary>停止任务并从引擎移除（可选择是否删除已下载文件）。</summary>
    public async Task DeleteAsync(bool deleteFile)
    {
        lock (_sync)
        {
            _deleting = true;
            if (State != NativeDownloadState.Completed)
                State = NativeDownloadState.Failed;
        }

        _pollTimer.Stop();

        TorrentManager? manager;
        lock (_sync) { manager = _manager; }

        if (manager is not null)
        {
            // 给 StopAsync 加超时，防止 MonoTorrent 卡死导致 UI 无响应。
            try
            {
                var stopTask = manager.StopAsync();
                if (await Task.WhenAny(stopTask, Task.Delay(5000)) != stopTask)
                {
                    // StopAsync 超时，直接继续清理。
                }
                else
                {
                    await stopTask; // 确保异常被观察。
                }
            }
            catch { /* 忽略 */ }

            try
            {
                await MonoTorrentEngine.Instance.RemoveAsync(manager,
                    deleteFile ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.KeepAllData);
            }
            catch { /* 忽略 */ }
            lock (_sync) { _manager = null; }
        }

        // 清理下载文件（StopAsync 可能超时导致 RemoveAsync 未执行，需手动清理）。
        if (deleteFile)
        {
            try
            {
                var dir = !string.IsNullOrWhiteSpace(ContainingDirectory) && System.IO.Directory.Exists(ContainingDirectory)
                    ? ContainingDirectory
                    : null;
                if (dir is not null)
                    System.IO.Directory.Delete(dir, recursive: true);
            }
            catch { /* 忽略 */ }
        }

        var task = _runTask;
        if (task is not null)
        {
            try { await task; } catch { /* 忽略 */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollTimer.Stop();
        _pollTimer.Dispose();

        TorrentManager? manager;
        lock (_sync) { manager = _manager; _manager = null; }

        if (manager is not null)
        {
            try { manager.StopAsync().GetAwaiter().GetResult(); } catch { /* 忽略 */ }
            try
            {
                MonoTorrentEngine.Instance.RemoveAsync(manager, RemoveMode.KeepAllData).GetAwaiter().GetResult();
            }
            catch { /* 忽略 */ }
        }
    }

    private async Task RunAsync()
    {
        try
        {
            // 1. 解析磁力链接
            if (!MagnetLink.TryParse(_magnetUrl, out var magnet))
            {
                SetFailed("无效的磁力链接");
                return;
            }

            // 2. 将任务加入共享引擎（若该链接的 metadata 已缓存则自动加载）
            var torrentSettings = new TorrentSettingsBuilder
            {
                MaximumConnections = _maxConnections,
                UploadSlots = 8,
                CreateContainingDirectory = true,
                AllowDht = true,
            }.ToSettings();
            var manager = await MonoTorrentEngine.Instance.AddAsync(magnet, _saveDirectory, torrentSettings);

            lock (_sync)
            {
                if (_deleting || _disposed)
                {
                    try { MonoTorrentEngine.Instance.RemoveAsync(manager, RemoveMode.KeepAllData).GetAwaiter().GetResult(); }
                    catch { /* 忽略 */ }
                    return;
                }
                _manager = manager;
                manager.TorrentStateChanged += OnTorrentStateChanged;
            }

            // 3. 等待 metadata（已缓存时立即返回）
            try
            {
                await manager.WaitForMetadataAsync();
                _metadataReady.SetResult(true);
            }
            catch
            {
                // Ignore errors during metadata wait, the task will still complete
            }

            // 4. 补充公共 Tracker：磁力链接只有 infohash，冷资源靠 DHT + tracker 才能快速找到 peer
            try
            {
                var trackerManager = manager.TrackerManager;
                var trackers = new[]
                {
                    new Uri("udp://tracker.opentrackr.org:1337/announce"),
                    new Uri("udp://open.stealth.si:80/announce"),
                    new Uri("udp://tracker.openbittorrent.com:6969/announce"),
                    new Uri("udp://open.demonii.com:1337/announce"),
                    new Uri("udp://exodus.desync.com:6969/announce"),
                    new Uri("udp://tracker.torrent.eu.org:451/announce"),
                    new Uri("udp://tracker.tiny-vps.com:6969/announce"),
                    new Uri("http://tracker.opentrackr.org:1337/announce"),
                };
                foreach (var uri in trackers)
                {
                    await trackerManager.AddTrackerAsync(uri);
                }
            }
            catch { /* 忽略 */ }

            lock (_sync)
            {
                if (_deleting || _disposed) return;
                var torrent = manager.Torrent;
                if (torrent is not null)
                {
                    Name = torrent.Name;
                    TotalBytes = torrent.Size;
                    ContainingDirectory = manager.ContainingDirectory;
                }
                else
                {
                    Name ??= magnet.Name ?? _preferredName ?? "磁力链接任务";
                }
            }

            // 4. 若暂停过则不再自动启动（由 Resume 恢复）
            bool shouldStart;
            lock (_sync) { shouldStart = State == NativeDownloadState.Downloading && !_deleting && !_disposed; }
            if (!shouldStart) return;

            // 5. 开始下载（自动 HashCheck 校验已有数据，实现断点续传）
            _pollTimer.Start();
            await manager.StartAsync();
        }
        catch (Exception ex)
        {
            SetFailed(ex.Message);
        }
    }

    private void OnTorrentStateChanged(object? sender, TorrentStateChangedEventArgs e)
    {
        switch (e.NewState)
        {
            case TorrentState.Seeding:
                // 下载完成进入做种状态，停止做种并上报完成
                try { _ = e.TorrentManager.StopAsync(); } catch { /* 忽略 */ }
                ReportCompleted();
                break;

            case TorrentState.Error:
                var manager = _manager;
                SetFailed(manager?.Error?.Exception?.Message ?? "下载失败");
                break;
        }
    }

    private void OnPollTick(object? sender, ElapsedEventArgs e)
    {
        TorrentManager? manager;
        lock (_sync) { manager = _manager; }
        if (manager is null) return;

        long total = TotalBytes;
        long downloaded;
        long speed;
        bool waitingForMetadata;

        lock (_sync)
        {
            if (manager.HasMetadata && manager.Torrent is { } torrent)
            {
                total = torrent.Size;
                TotalBytes = total;
            }

            if (total > 0)
            {
                downloaded = (long)(total * manager.Progress / 100.0);
                waitingForMetadata = false;
            }
            else
            {
                // metadata 未就绪：使用底层已接收字节数，而非 0。
                downloaded = manager.Monitor.DataBytesReceived;
                waitingForMetadata = true;
            }

            DownloadedBytes = downloaded;
            speed = manager.Monitor.DownloadRate;
            Speed = speed;
        }

        ProgressChanged?.Invoke(this, new NativeDownloadProgress(downloaded, total, speed, waitingForMetadata));
    }

    private void ReportCompleted()
    {
        lock (_sync)
        {
            if (_completed || _deleting || _disposed) return;
            _completed = true;
            State = NativeDownloadState.Completed;
        }
        _pollTimer.Stop();
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void SetFailed(string error)
    {
        lock (_sync)
        {
            if (_deleting || _disposed) return;
            Error = error;
            State = NativeDownloadState.Failed;
        }
        _pollTimer.Stop();
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
