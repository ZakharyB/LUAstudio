using System.Collections.Concurrent;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Documents;

public interface IDocumentSnapshotStore
{
    SourceSnapshot? GetSnapshot(Guid documentId);

    SourceSnapshot UpdateContent(Guid documentId, string content, string? filePath, LuaDialect dialect);

    void Remove(Guid documentId);
}

public sealed class DocumentSnapshotStore : IDocumentSnapshotStore
{
    private readonly ConcurrentDictionary<Guid, (int Version, SourceSnapshot Snapshot)> _snapshots = new();

    public SourceSnapshot? GetSnapshot(Guid documentId) =>
        _snapshots.TryGetValue(documentId, out var entry) ? entry.Snapshot : null;

    public SourceSnapshot UpdateContent(Guid documentId, string content, string? filePath, LuaDialect dialect)
    {
        var version = 1;
        if (_snapshots.TryGetValue(documentId, out var existing))
        {
            version = existing.Version + 1;
        }

        var snapshot = new SourceSnapshot(documentId, version, SourceText.From(content), filePath, dialect);
        _snapshots[documentId] = (version, snapshot);
        return snapshot;
    }

    public void Remove(Guid documentId) => _snapshots.TryRemove(documentId, out _);
}
