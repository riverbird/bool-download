using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BoolDownload.Models;

public static class AppVersion
{
    public const int VersionCode = 202609151;
}

public class UpdateInfo
{
    [JsonPropertyName("app")]
    public AppInfo? App { get; set; }

    [JsonPropertyName("changelog")]
    public List<string>? Changelog { get; set; }

    [JsonPropertyName("updatePrompt")]
    public UpdatePrompt? UpdatePrompt { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }

    [JsonPropertyName("deprecatedMessage")]
    public string? DeprecatedMessage { get; set; }

    [JsonPropertyName("windows_x64")]
    public PlatformInfo? WindowsX64 { get; set; }

    [JsonPropertyName("deb_x64")]
    public PlatformInfo? DebX64 { get; set; }

    [JsonPropertyName("deb_arm64")]
    public PlatformInfo? DebArm64 { get; set; }

    [JsonPropertyName("rpm_x64")]
    public PlatformInfo? RpmX64 { get; set; }

    [JsonPropertyName("rpm_arm64")]
    public PlatformInfo? RpmArm64 { get; set; }

    public PlatformInfo? GetPlatformInfo(string key) => key switch
    {
        "windows_x64" => WindowsX64,
        "deb_x64" => DebX64,
        "deb_arm64" => DebArm64,
        "rpm_x64" => RpmX64,
        "rpm_arm64" => RpmArm64,
        _ => null
    };
}

public class AppInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("appId")]
    public string? AppId { get; set; }
}

public class PlatformInfo
{
    [JsonPropertyName("latestVersion")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("versionCode")]
    public int VersionCode { get; set; }

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("forceUpdate")]
    public bool ForceUpdate { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("download")]
    public DownloadInfo? Download { get; set; }
}

public class DownloadInfo
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("checksumSha256")]
    public string? ChecksumSha256 { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

public class UpdatePrompt
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
