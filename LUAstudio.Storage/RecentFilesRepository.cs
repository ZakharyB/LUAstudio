using Microsoft.Data.Sqlite;

namespace LUAstudio.Storage;

public sealed class RecentFilesRepository : IRecentFilesRepository
{
    private readonly IAppDatabase _database;

    public RecentFilesRepository(IAppDatabase database)
    {
        _database = database;
    }

    public async Task RecordOpenAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO recent_files (path, last_opened_utc) VALUES ($path, $utc)
            ON CONFLICT(path) DO UPDATE SET last_opened_utc = excluded.last_opened_utc;
            """;
        cmd.Parameters.AddWithValue("$path", fullPath);
        cmd.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var trim = connection.CreateCommand();
        trim.CommandText = """
            DELETE FROM recent_files
            WHERE rowid NOT IN (
                SELECT rowid FROM recent_files
                ORDER BY last_opened_utc DESC
                LIMIT 20
            );
            """;
        await trim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetRecentAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT path FROM recent_files
            ORDER BY last_opened_utc DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", maxCount);

        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(reader.GetString(0));
        }

        return list;
    }
}
