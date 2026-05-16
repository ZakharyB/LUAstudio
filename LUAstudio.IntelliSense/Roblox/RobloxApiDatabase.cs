using System.Collections.Frozen;
using System.Text.Json;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.Storage;

namespace LUAstudio.IntelliSense.Roblox;

/// <summary>
/// Lazy-loaded Roblox API metadata. Ships with embedded core services; can ingest API dump JSON at runtime.
/// </summary>
public sealed class RobloxApiDatabase : IRobloxApiDatabase
{
    private FrozenDictionary<string, RobloxMember> _globals = FrozenDictionary<string, RobloxMember>.Empty;
    private FrozenDictionary<string, RobloxClass> _classes = FrozenDictionary<string, RobloxClass>.Empty;
    private int _loaded;

    public bool TryGetGlobal(string name, out RobloxMember member) => _globals.TryGetValue(name, out member!);

    public bool TryGetService(string serviceName, out RobloxClass service) =>
        _classes.TryGetValue(serviceName, out service!);

    public bool TryGetMember(string className, string memberName, out RobloxMember member)
    {
        member = null!;
        if (!_classes.TryGetValue(className, out var cls))
        {
            return false;
        }

        return cls.Members.FirstOrDefault(m => m.Name == memberName) is { } found && (member = found) is not null;
    }

    public IReadOnlyList<RobloxMember> GetMembers(string className) =>
        _classes.TryGetValue(className, out var cls) ? cls.Members : Array.Empty<RobloxMember>();

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _loaded, 1, 0) != 0)
        {
            return;
        }

        await Task.Run(() =>
        {
            var cachePath = Path.Combine(LuaStudioPaths.CacheDirectory, "roblox-api.json");
            if (File.Exists(cachePath))
            {
                LoadFromJson(File.ReadAllText(cachePath));
            }
            else
            {
                LoadBuiltInCore();
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public void IngestApiDumpJson(string json)
    {
        LoadFromJson(json);
        Directory.CreateDirectory(LuaStudioPaths.CacheDirectory);
        File.WriteAllText(Path.Combine(LuaStudioPaths.CacheDirectory, "roblox-api.json"), json);
        Interlocked.Exchange(ref _loaded, 1);
    }

    private void LoadBuiltInCore()
    {
        var globals = new Dictionary<string, RobloxMember>(StringComparer.Ordinal)
        {
            ["game"] = new RobloxMember("game", SymbolKind.Service, "DataModel root."),
            ["workspace"] = new RobloxMember("workspace", SymbolKind.Service, "Workspace service."),
            ["script"] = new RobloxMember("script", SymbolKind.Class, "Base script instance."),
        };

        var instanceMembers = new List<RobloxMember>
        {
            new("Name", SymbolKind.Property, "Instance name."),
            new("Parent", SymbolKind.Property, "Parent instance."),
            new("FindFirstChild", SymbolKind.Method, "Finds a child by name.", "Instance?"),
            new("WaitForChild", SymbolKind.Method, "Yields until child exists.", "Instance"),
            new("GetChildren", SymbolKind.Method, "Returns child instances.", "{Instance}"),
            new("Destroy", SymbolKind.Method, "Destroys the instance."),
            new("Clone", SymbolKind.Method, "Clones the instance.", "Instance"),
        };

        var classes = new Dictionary<string, RobloxClass>(StringComparer.Ordinal)
        {
            ["DataModel"] = new RobloxClass("DataModel", "Roblox game hierarchy root.", new[]
            {
                new RobloxMember("Workspace", SymbolKind.Property, "Workspace container."),
                new RobloxMember("GetService", SymbolKind.Method, "Gets a service by name.", "Instance"),
            }),
            ["Workspace"] = new RobloxClass("Workspace", "3D world container.", instanceMembers),
            ["Instance"] = new RobloxClass("Instance", "Base Roblox object.", instanceMembers),
            ["Players"] = new RobloxClass("Players", "Player service.", new[]
            {
                new RobloxMember("LocalPlayer", SymbolKind.Property, "Local client player."),
                new RobloxMember("GetPlayerFromCharacter", SymbolKind.Method, "Maps character to player.", "Player?"),
            }),
            ["Player"] = new RobloxClass("Player", "Player instance.", new[]
            {
                new RobloxMember("Character", SymbolKind.Property, "Player character model."),
                new RobloxMember("UserId", SymbolKind.Property, "Roblox user id.", "number"),
            }),
        };

        _globals = globals.ToFrozenDictionary(StringComparer.Ordinal);
        _classes = classes.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private void LoadFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Classes", out var classesEl))
            {
                LoadBuiltInCore();
                return;
            }

            var globals = new Dictionary<string, RobloxMember>(StringComparer.Ordinal);
            var classes = new Dictionary<string, RobloxClass>(StringComparer.Ordinal);

            foreach (var cls in classesEl.EnumerateArray())
            {
                var name = cls.GetProperty("Name").GetString() ?? "";
                var members = new List<RobloxMember>();
                if (cls.TryGetProperty("Members", out var membersEl))
                {
                    foreach (var m in membersEl.EnumerateArray())
                    {
                        var memberName = m.GetProperty("Name").GetString() ?? "";
                        var kind = m.TryGetProperty("MemberType", out var mt)
                            ? MapMemberType(mt.GetString())
                            : SymbolKind.Property;
                        members.Add(new RobloxMember(memberName, kind, null));
                    }
                }

                classes[name] = new RobloxClass(name, null, members);
            }

            _globals = globals.ToFrozenDictionary(StringComparer.Ordinal);
            _classes = classes.ToFrozenDictionary(StringComparer.Ordinal);
        }
        catch
        {
            LoadBuiltInCore();
        }
    }

    private static SymbolKind MapMemberType(string? memberType) => memberType switch
    {
        "Function" => SymbolKind.Method,
        "Event" => SymbolKind.Method,
        "Property" => SymbolKind.Property,
        _ => SymbolKind.Field
    };
}
