using System;
using Microsoft.Data.Sqlite;

namespace BoolDownload.Services;

/// <summary>
/// 界面选项的 SQLite 存储：~/.booldownload/booldownload.db，数据表 option(name, value)。
/// </summary>
public static class OptionRepository
{
    public static void EnsureTable()
    {
        using var connection = AppDatabase.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS option (
                name  TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL DEFAULT ''
            );
            """;
        command.ExecuteNonQuery();
    }

    public static string? Get(string name)
    {
        try
        {
            EnsureTable();

            using var connection = AppDatabase.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM option WHERE name = $name;";
            command.Parameters.AddWithValue("$name", name);

            var value = command.ExecuteScalar();
            return value is DBNull or null ? null : Convert.ToString(value);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string name, string value)
    {
        try
        {
            EnsureTable();

            using var connection = AppDatabase.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO option (name, value) VALUES ($name, $value)
                ON CONFLICT(name) DO UPDATE SET value = excluded.value;
                """;
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignore persistence failures.
        }
    }
}
