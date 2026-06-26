using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LUAstudio.ExecutionHost.Runtime;

public sealed class ExecutionTraceRecorder
{
    private readonly List<TraceEvent> _events = [];
    private readonly object _lock = new();

    public void RecordMockCall(string api, string detail)
    {
        lock (_lock)
        {
            _events.Add(new TraceEvent(DateTime.UtcNow, "mock", api, detail));
        }
    }

    public void RecordBreakpoint(int line, string? sourcePath)
    {
        lock (_lock)
        {
            _events.Add(new TraceEvent(DateTime.UtcNow, "breakpoint", sourcePath ?? string.Empty, line.ToString()));
        }
    }

    public void RecordStep(string reason, int line, string? sourcePath)
    {
        lock (_lock)
        {
            _events.Add(new TraceEvent(DateTime.UtcNow, "step", reason, $"{sourcePath}:{line}"));
        }
    }

    public string ComputeModulesHash(IReadOnlyDictionary<string, string> modules)
    {
        var builder = new StringBuilder();
        foreach (var pair in modules.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(pair.Key);
            builder.Append('\0');
            builder.Append(pair.Value);
            builder.Append('\0');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    public TraceSnapshot CreateSnapshot(Guid sessionId, string modulesHash, int randomSeed) =>
        new(sessionId, modulesHash, randomSeed, _events.ToArray());

    public static void SaveSnapshot(TraceSnapshot snapshot, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{snapshot.SessionId:N}.trace.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static TraceSnapshot? LoadSnapshot(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TraceSnapshot>(File.ReadAllText(path));
    }
}

public sealed record TraceEvent(DateTime TimestampUtc, string Kind, string Target, string Detail);

public sealed record TraceSnapshot(
    Guid SessionId,
    string ModulesHash,
    int RandomSeed,
    IReadOnlyList<TraceEvent> Events);
