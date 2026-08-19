using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BoolDownload.Services;

public sealed class SavedDownloadTask
{
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Done { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public double Progress { get; set; }
    public DateTime AddedTime { get; set; }
    public string Url { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public ulong TaskId { get; set; }
    public int MaxConnections { get; set; }
}

public static class DownloadTaskStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "BoolDownload",
        "download-tasks.json");

    public static List<SavedDownloadTask> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<SavedDownloadTask>();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<SavedDownloadTask>>(json, Options)
                   ?? new List<SavedDownloadTask>();
        }
        catch
        {
            return new List<SavedDownloadTask>();
        }
    }

    public static void Save(List<SavedDownloadTask> tasks)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(tasks, Options));
        }
        catch
        {
            // Ignore persistence failures.
        }
    }
}