using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace BoolDownload.Services;

/// <summary>下载引擎类型。</summary>
public enum DownloadEngine
{
    /// <summary>迅雷开放下载引擎。</summary>
    Xunlei = 0,

    /// <summary>原生下载引擎（.NET HttpClient）。</summary>
    Native = 1,

    /// <summary>原生下载引擎（Downloader 包）。</summary>
    Downloader = 2,
}

/// <summary>原生下载任务状态。</summary>
public enum NativeDownloadState
{
    Pending = 0,
    Downloading,
    Paused,
    Completed,
    Failed,
}

/// <summary>原生下载进度。</summary>
public sealed class NativeDownloadProgress
{
    public long DownloadedBytes { get; }
    public long TotalBytes { get; }
    public long Speed { get; }

    public NativeDownloadProgress(long downloadedBytes, long totalBytes, long speed)
    {
        DownloadedBytes = downloadedBytes;
        TotalBytes = totalBytes;
        Speed = speed;
    }
}

/// <summary>
/// 原生下载引擎：基于 .NET HttpClient 与 async/await（Task 与线程池）实现。
/// 支持 HTTP Range 分段并行下载（按“最大连接数”分段），
/// 通过分片临时文件（文件名.partN）实现断点续传，
/// 全部完成后按序合并为最终文件。
/// 全程不接触 UI 线程，进度通过事件上报，由调用方负责切换到 UI 线程。
/// </summary>
public sealed class NativeDownload : IDisposable
{
    private const int BufferSize = 81920; // 80 KB

    private readonly object _sync = new();
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly string _directory;
    private readonly string _fileName;
    private readonly int _segmentCount;
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private long _totalBytes = -1;
    private long _downloadedBytes;
    private long _speed;
    private long _lastTick;
    private long _lastBytes;
    private bool _disposed;

    /// <summary>当前任务状态。</summary>
    public NativeDownloadState State { get; private set; } = NativeDownloadState.Pending;

    /// <summary>失败时的错误信息。</summary>
    public string? Error { get; private set; }

    public long TotalBytes => Interlocked.Read(ref _totalBytes);

    public long DownloadedBytes => Interlocked.Read(ref _downloadedBytes);

    /// <summary>下载过程中周期性上报进度（后台线程触发，需自行切换到 UI 线程）。</summary>
    public event EventHandler<NativeDownloadProgress>? ProgressChanged;

    /// <summary>任务结束（完成/暂停/失败）时触发（后台线程触发，需自行切换到 UI 线程）。</summary>
    public event EventHandler? Completed;

    public NativeDownload(string url, string directory, string fileName, int segmentCount)
    {
        _url = url;
        _directory = directory;
        _fileName = fileName;
        _segmentCount = Math.Clamp(segmentCount, 1, 128);
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    private string PartPath(int index) => Path.Combine(_directory, $"{_fileName}.part{index}");

    private string FinalPath => Path.Combine(_directory, _fileName);

    /// <summary>开始或继续下载（后台线程执行，不阻塞调用线程）。</summary>
    public void Start()
    {
        CancellationToken token;
        lock (_sync)
        {
            if (State == NativeDownloadState.Downloading)
                return;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            token = _cts.Token;
            State = NativeDownloadState.Downloading;
        }
        _runTask = Task.Run(() => RunAsync(token));
    }

    /// <summary>暂停下载（保留分片，支持断点续传）。</summary>
    public void Pause()
    {
        CancellationTokenSource? cts;
        lock (_sync) cts = _cts;
        try { cts?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    /// <summary>取消并删除下载产物（分片与最终文件）。</summary>
    public async Task DeleteAsync(bool deleteFile)
    {
        CancellationTokenSource? cts;
        lock (_sync) cts = _cts;
        try { cts?.Cancel(); }
        catch (ObjectDisposedException) { }

        if (_runTask is not null)
        {
            try { await _runTask; }
            catch { }
        }

        for (var i = 0; i < _segmentCount; i++)
            TryDeleteFile(PartPath(i));
        if (deleteFile)
            TryDeleteFile(FinalPath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            Directory.CreateDirectory(_directory);

            // 断点续传：统计已有分片大小作为已下载字节数。
            long existingBytes = 0;
            for (var i = 0; i < _segmentCount; i++)
            {
                var part = PartPath(i);
                if (File.Exists(part))
                    existingBytes += new FileInfo(part).Length;
            }
            Interlocked.Exchange(ref _downloadedBytes, existingBytes);
            Interlocked.Exchange(ref _lastBytes, existingBytes);
            Interlocked.Exchange(ref _lastTick, Environment.TickCount64);

            var (canRange, total) = await ProbeAsync(token);
            if (total < 0)
                throw new InvalidOperationException("无法获取文件大小");
            Interlocked.Exchange(ref _totalBytes, total);

            if (!canRange)
            {
                // 服务器不支持 Range：清空分片后整段下载，避免续传造成数据损坏。
                for (var i = 0; i < _segmentCount; i++)
                    TryDeleteFile(PartPath(i));
                Interlocked.Exchange(ref _downloadedBytes, 0);
                Interlocked.Exchange(ref _lastBytes, 0);
            }

            var segmentCount = canRange ? _segmentCount : 1;
            var segments = BuildSegments(segmentCount, total, token);
            if (segments.Count > 0)
            {
                RaiseProgress();
                var tasks = segments.Select(s => DownloadSegmentAsync(s, token)).ToArray();
                await Task.WhenAll(tasks);
                RaiseProgress();
            }

            token.ThrowIfCancellationRequested();
            await MergePartsAsync(segmentCount);

            lock (_sync) State = NativeDownloadState.Completed;
        }
        catch (OperationCanceledException)
        {
            // 用户暂停：保留分片以便续传。
            lock (_sync) State = NativeDownloadState.Paused;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            lock (_sync) State = NativeDownloadState.Failed;
        }
        finally
        {
            RaiseProgress(force: true);
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>探测总大小与服务器是否支持 Range。</summary>
    private async Task<(bool CanRange, long Total)> ProbeAsync(CancellationToken token)
    {
        long total = -1;

        using (var head = new HttpRequestMessage(HttpMethod.Head, _url))
        using (var response = await _http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, token))
        {
            if (response.IsSuccessStatusCode)
                total = response.Content.Headers.ContentLength ?? -1;
        }

        var canRange = false;
        using (var probe = new HttpRequestMessage(HttpMethod.Get, _url))
        {
            probe.Headers.Range = new RangeHeaderValue(0, 0);
            using var response = await _http.SendAsync(probe, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                canRange = true;
                if (total < 0)
                    total = response.Content.Headers.ContentRange?.Length ?? -1;
            }
            else if (total < 0 && response.IsSuccessStatusCode)
            {
                total = response.Content.Headers.ContentLength ?? -1;
            }
        }

        return (canRange, total);
    }

    /// <summary>按总大小切分 Range 段，并依据已有分片大小跳过已完成段、调整续传起点。</summary>
    private List<(long Start, long End, int Index)> BuildSegments(int segmentCount, long total, CancellationToken token)
    {
        var segments = new List<(long, long, int)>();
        if (total <= 0)
            return segments;

        var chunk = total / segmentCount;
        for (var i = 0; i < segmentCount; i++)
        {
            var start = i * chunk;
            var end = i == segmentCount - 1 ? total - 1 : (i + 1) * chunk - 1;
            if (end < start)
                continue;

            var length = end - start + 1;
            var existing = GetPartSize(i);
            if (existing >= length)
                continue; // 该分片已完整下载
            if (existing > 0)
                start += existing; // 断点续传
            segments.Add((start, end, i));
        }
        return segments;
    }

    private async Task DownloadSegmentAsync((long Start, long End, int Index) segment, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _url);
        request.Headers.Range = new RangeHeaderValue(segment.Start, segment.End);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        // 若服务器忽略 Range 返回整段（200），则重建分片文件而非追加，避免数据损坏。
        var partial = response.StatusCode == HttpStatusCode.PartialContent;
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        await using var file = new FileStream(
            PartPath(segment.Index),
            partial ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            BufferSize,
            useAsync: true);

        var buffer = new byte[BufferSize];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), token);
            Interlocked.Add(ref _downloadedBytes, read);
            RaiseProgress();
        }
    }

    private async Task MergePartsAsync(int segmentCount)
    {
        await using var output = new FileStream(FinalPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        for (var i = 0; i < segmentCount; i++)
        {
            var part = PartPath(i);
            if (!File.Exists(part))
                throw new InvalidOperationException($"缺少分片 {i}");
            await using var input = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            await input.CopyToAsync(output);
        }
        for (var i = 0; i < segmentCount; i++)
            TryDeleteFile(PartPath(i));
    }

    private long GetPartSize(int index)
    {
        try
        {
            var path = PartPath(index);
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void RaiseProgress(bool force = false)
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastTick);
        if (!force && now - last < 200)
            return;
        if (Interlocked.CompareExchange(ref _lastTick, now, last) != last)
            return;

        var bytes = Interlocked.Read(ref _downloadedBytes);
        var previous = Interlocked.Exchange(ref _lastBytes, bytes);
        var elapsed = now - last;
        var speed = elapsed > 0 ? (bytes - previous) * 1000 / elapsed : 0;
        if (speed < 0) speed = 0;
        Interlocked.Exchange(ref _speed, speed);

        ProgressChanged?.Invoke(this, new NativeDownloadProgress(
            Interlocked.Read(ref _downloadedBytes),
            Interlocked.Read(ref _totalBytes),
            Interlocked.Read(ref _speed)));
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
