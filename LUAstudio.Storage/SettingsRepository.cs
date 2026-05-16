using Microsoft.Data.Sqlite;

namespace LUAstudio.Storage;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly IAppDatabase _database;

    public SettingsRepository(IAppDatabase database)
    {
        _database = database;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is DBNull or null ? null : (string)result;
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        if (value is null)
        {
            await using var del = connection.CreateCommand();
            del.CommandText = "DELETE FROM settings WHERE key = $key;";
            del.Parameters.AddWithValue("$key", key);
            await del.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var upsert = connection.CreateCommand();
        upsert.CommandText = """
            INSERT INTO settings (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        upsert.Parameters.AddWithValue("$key", key);
        upsert.Parameters.AddWithValue("$value", value);
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
