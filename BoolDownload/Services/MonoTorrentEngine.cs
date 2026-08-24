using System;
using System.IO;
using MonoTorrent;
using MonoTorrent.Client;

namespace BoolDownload.Services;

/// <summary>
/// 共享的 MonoTorrent 客户端引擎。磁力链接与 BT 种子下载共用同一个
/// <see cref="ClientEngine"/>，避免重复监听端口与 DHT 缓存冲突。
/// </summary>
internal static class MonoTorrentEngine
{
    /// <summary>全局共享的 MonoTorrent 客户端引擎（应用生命周期内常驻）。</summary>
    public static ClientEngine Instance { get; } = CreateEngine();

    private static ClientEngine CreateEngine()
    {
        var cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BoolDownload", "MonoTorrentCache");
        try { Directory.CreateDirectory(cacheRoot); } catch { /* 忽略 */ }

        var settings = new EngineSettingsBuilder
        {
            CacheDirectory = cacheRoot,
            AutoSaveLoadMagnetLinkMetadata = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadDhtCache = true,
            AllowPortForwarding = false,
        }.ToSettings();

        return new ClientEngine(settings);
    }
}
