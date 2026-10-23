using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace BoolDownload.Services;

/// <summary>
/// 应用配置的 SQLite 数据库：~/.booldownload/booldownload.db。
/// </summary>
public static class AppDatabase
{
    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".booldownload");

    public static string DatabasePath { get; } = Path.Combine(DirectoryPath, "booldownload.db");

    public static SqliteConnection OpenConnection()
    {
        Directory.CreateDirectory(DirectoryPath);

        var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();
        return connection;
    }
}
