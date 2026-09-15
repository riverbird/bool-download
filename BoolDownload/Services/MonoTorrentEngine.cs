using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections;

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

            // 固定端口：BT 是 tit-for-tat 协议，端口不通只能主动连别人，握手慢、被 choke 概率高。
            // 用户需在路由器做端口映射（TCP+UDP 55123）并在防火墙放行。
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["IPv4"] = new IPEndPoint(IPAddress.Any, 55123),
            },
            DhtEndPoint = new IPEndPoint(IPAddress.Any, 55123),

            // 不限速：上传被限到很低时，其他 peer 会优先拒绝给你数据。
            MaximumDownloadRate = 0,
            MaximumUploadRate = 0,

            // 连接数上限
            MaximumConnections = 500,
            MaximumHalfOpenConnections = 50,

            // UPnP/NAT-PMP 自动端口映射（比手动映射方便，但不如手动可靠）
            AllowPortForwarding = true,

            // DHT 缓存持久化：磁力链接完全依赖 DHT 找 peer，首次运行要缓存，否则前几分钟没速度。
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,

            // 局域网发现
            AllowLocalPeerDiscovery = true,

            // 加密协商：某些 ISP 会识别并限速 BT 流量，开启加密可规避。
            // 列表顺序即优先级：先尝试 RC4Header（快），失败再用 RC4Full（全加密），最后降级明文。
            AllowedEncryption = new List<EncryptionType>
            {
                EncryptionType.RC4Header,
                EncryptionType.RC4Full,
                EncryptionType.PlainText,
            },

            // 写入缓存：缓解机械硬盘随机写入瓶颈（约 10 MB）
            DiskCacheBytes = 10 * 1024 * 1024,
        }.ToSettings();

        return new ClientEngine(settings);
    }
}
