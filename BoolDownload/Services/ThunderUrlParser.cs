using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BoolDownload.Services;

/// <summary>
/// 解析 thunder:// 开头的迅雷专用下载链接。
/// 优先调用迅雷开放下载引擎提供的 XL_ParseThunderPrivateUrl 原生接口，
/// 将专用链接还原为普通 http/https/ftp 直链；
/// 若当前运行环境的内置原生库未导出该符号，则回退到标准的
/// thunder:// 协议解码算法（Base64(AA + URL + ZZ)）。
/// </summary>
public static class ThunderUrlParser
{
    private const string ThunderScheme = "thunder://";
    private const string Prefix = "AA";
    private const string Suffix = "ZZ";

    /// <summary>
    /// 迅雷开放下载引擎接口：解析 thunder:// 专用链接。
    /// 成功返回 TRUE（非 0），解析结果写入 normalUrlBuffer。
    /// </summary>
    [DllImport("dk", EntryPoint = "XL_ParseThunderPrivateUrl",
        CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int XL_ParseThunderPrivateUrl(
        [MarshalAs(UnmanagedType.LPWStr)] string thunderUrl,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder normalUrlBuffer,
        int bufferSize);

    /// <summary>判断输入的链接是否为 thunder:// 专用链接。</summary>
    public static bool IsThunderUrl(string url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
               url.TrimStart().StartsWith(ThunderScheme, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 尝试将 thunder:// 专用链接解析为普通 http/https/ftp 直链。
    /// 非 thunder:// 链接直接返回 false。
    /// </summary>
    public static bool TryParse(string input, out string url)
    {
        url = string.Empty;
        if (!IsThunderUrl(input))
            return false;

        var thunderUrl = input.Trim();
        if (TryParseNative(thunderUrl, out var native) && IsHttpLikeUrl(native))
        {
            url = native;
            return true;
        }

        return TryParseManaged(thunderUrl, out url);
    }

    private static bool TryParseNative(string thunderUrl, out string url)
    {
        url = string.Empty;
        try
        {
            var buffer = new StringBuilder(4096);
            var result = XL_ParseThunderPrivateUrl(thunderUrl, buffer, buffer.Capacity);
            if (result == 0 || buffer.Length == 0)
                return false;
            url = buffer.ToString().TrimEnd('\0').Trim();
            return url.Length > 0;
        }
        catch (Exception)
        {
            // 内置原生库（libdk.so / libdk.dylib / dk.dll）未导出该接口时，
            // 交由托管实现解析。
            return false;
        }
    }

    private static bool TryParseManaged(string thunderUrl, out string url)
    {
        url = string.Empty;
        try
        {
            var payload = thunderUrl.Substring(ThunderScheme.Length);
            var bytes = DecodeBase64(payload);
            if (bytes is null || bytes.Length == 0)
                return false;

            var decoded = Encoding.UTF8.GetString(bytes);
            if (decoded.StartsWith(Prefix, StringComparison.Ordinal))
                decoded = decoded.Substring(Prefix.Length);
            if (decoded.EndsWith(Suffix, StringComparison.Ordinal))
                decoded = decoded.Substring(0, decoded.Length - Suffix.Length);

            decoded = decoded.Trim();
            if (!IsHttpLikeUrl(decoded))
                return false;

            url = decoded;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static byte[]? DecodeBase64(string payload)
    {
        var text = payload.Trim();
        // 部分链接可能缺少 Base64 填充符，这里补齐后解码。
        switch (text.Length % 4)
        {
            case 2:
                text += "==";
                break;
            case 3:
                text += "=";
                break;
        }
        return Convert.FromBase64String(text);
    }

    private static bool IsHttpLikeUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase);
    }
}
