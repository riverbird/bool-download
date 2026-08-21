using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BoolDownload.Services;

/// <summary>
/// 保存位置下拉列表的本地 JSON 持久化，
/// 每次打开“创建新下载”窗口时自动加载。
/// </summary>
public static class FolderStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "BoolDownload",
        "folders.json");

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<string>();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<string>>(json, Options)
                   ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void Save(List<string> folders)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(folders, Options));
        }
        catch
        {
            // Ignore persistence failures.
        }
    }
}
