using Microsoft.Data.Sqlite;

namespace LUAstudio.Storage;

public sealed class WorkspaceRootsRepository : IWorkspaceRootsRepository
{
    private readonly IAppDatabase _database;

    public WorkspaceRootsRepository(IAppDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<WorkspaceRootRecord>> GetOrderedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, path, sort_order, added_utc
            FROM workspace_roots
            ORDER BY sort_order ASC, id ASC;
            """;

        var list = new List<WorkspaceRootRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            var path = reader.GetString(1);
            var order = reader.GetInt32(2);
            var added = DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind);
            list.Add(new WorkspaceRootRecord(id, path, order, added));
        }

        return list;
    }

    public async Task ReplaceAllAsync(IReadOnlyList<string> orderedFullPaths, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = (SqliteTransaction)tx;
            clear.CommandText = "DELETE FROM workspace_roots;";
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var order = 0;
        foreach (var path in orderedFullPaths)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)tx;
            insert.CommandText = """
                INSERT INTO workspace_roots (path, sort_order, added_utc)
                VALUES ($path, $order, $added);
                """;
            insert.Parameters.AddWithValue("$path", path);
            insert.Parameters.AddWithValue("$order", order++);
            insert.Parameters.AddWithValue("$added", DateTimeOffset.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
