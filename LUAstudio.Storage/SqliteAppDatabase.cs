using Microsoft.Data.Sqlite;

namespace LUAstudio.Storage;

public sealed class SqliteAppDatabase : IAppDatabase
{
    private readonly object _initLock = new();
    private bool _initialized;

    public string DatabasePath => LuaStudioPaths.DatabasePath;

    public SqliteConnection OpenConnection()
    {
        EnsureInitialized();
        var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void EnsureInitialized()
    {
        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            using var connection = new SqliteConnection($"Data Source={DatabasePath}");
            connection.Open();
            using (var wal = connection.CreateCommand())
            {
                wal.CommandText = "PRAGMA journal_mode = WAL;";
                wal.ExecuteNonQuery();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_version (
                    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
                    version INTEGER NOT NULL
                );
                INSERT OR IGNORE INTO schema_version (singleton, version) VALUES (1, 1);

                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY NOT NULL,
                    value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS workspace_roots (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    path TEXT NOT NULL UNIQUE,
                    sort_order INTEGER NOT NULL,
                    added_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS recent_files (
                    path TEXT PRIMARY KEY NOT NULL,
                    last_opened_utc TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
            _initialized = true;
        }
    }
}
