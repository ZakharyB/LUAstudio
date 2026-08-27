using System.Collections.Frozen;
using LUAstudio.Storage;

namespace LUAstudio.IntelliSense.Roblox;

public sealed class RobloxApiDatabase : IRobloxApiDatabase
{
    private FrozenDictionary<string, RobloxClass> _classes = FrozenDictionary<string, RobloxClass>.Empty;
    private FrozenDictionary<string, RobloxMember> _globals = FrozenDictionary<string, RobloxMember>.Empty;
    private FrozenDictionary<string, string> _aliases = FrozenDictionary<string, string>.Empty;
    private int _loaded;

    public IReadOnlyDictionary<string, string> GlobalTypeAliases => _aliases;

    public IReadOnlyList<string> ServiceNames { get; private set; } = Array.Empty<string>();

    public bool TryGetGlobal(string name, out RobloxMember member) => _globals.TryGetValue(name, out member!);

    public bool TryGetClass(string className, out RobloxClass service) =>
        _classes.TryGetValue(className, out service!);

    public bool TryGetMember(string className, string memberName, out RobloxMember member)
    {
        member = null!;
        var found = GetMembers(className, includeInherited: true).FirstOrDefault(m => m.Name == memberName);
        if (found is null)
        {
            return false;
        }

        member = found;
        return true;
    }

    public IReadOnlyList<RobloxMember> GetMembers(string className, bool includeInherited = true)
    {
        var result = new List<RobloxMember>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = className;

        while (!string.IsNullOrEmpty(current) && _classes.TryGetValue(current, out var cls))
        {
            foreach (var m in cls.Members)
            {
                if (seen.Add(m.Name))
                {
                    result.Add(m);
                }
            }

            if (!includeInherited)
            {
                break;
            }

            current = cls.SuperClass ?? string.Empty;
        }

        return result;
    }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _loaded, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                var bundled = Path.Combine(AppContext.BaseDirectory, "Assets", "Roblox", "api-dump.json");
                var cachePath = Path.Combine(LuaStudioPaths.CacheDirectory, "roblox-api.json");

                string json;
                if (File.Exists(cachePath))
                {
                    json = File.ReadAllText(cachePath);
                }
                else if (File.Exists(bundled))
                {
                    json = File.ReadAllText(bundled);
                    Directory.CreateDirectory(LuaStudioPaths.CacheDirectory);
                    File.WriteAllText(cachePath, json);
                }
                else
                {
                    Apply(ApiDumpIngestor.BuildBuiltInFallback());
                    return;
                }

                Apply(ApiDumpIngestor.Ingest(json));
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref _loaded, 0);
            throw;
        }
        catch
        {
            // A bad/stale cache must not take down completion (notably after ':').
            // Fall back to the built-in API and allow a later reload to replace it.
            Apply(ApiDumpIngestor.BuildBuiltInFallback());
        }
    }

    public async Task ReloadFromPathAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        await Task.Run(() =>
        {
            Apply(ApiDumpIngestor.Ingest(File.ReadAllText(path)));
            Interlocked.Exchange(ref _loaded, 1);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadLatestAsync(string? url = null, CancellationToken cancellationToken = default)
    {
        url ??= "https://raw.githubusercontent.com/MaximumADHD/Roblox-Client-Tracker/roblox/Reflection/Metadata.json";
        using var client = new HttpClient();
        var json = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(LuaStudioPaths.CacheDirectory);
        var cachePath = Path.Combine(LuaStudioPaths.CacheDirectory, "roblox-api.json");
        await File.WriteAllTextAsync(cachePath, json, cancellationToken).ConfigureAwait(false);
        Apply(ApiDumpIngestor.Ingest(json));
        Interlocked.Exchange(ref _loaded, 1);
    }

    private void Apply(ApiDumpIngestResult result)
    {
        _classes = result.Classes;
        _globals = result.Globals;
        _aliases = result.GlobalTypeAliases;
        ServiceNames = RobloxGlobalTypes.Services.ToArray();
    }
}
