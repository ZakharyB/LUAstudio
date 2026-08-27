using System.Collections.Frozen;
using System.Text.Json;
using LUAstudio.IntelliSense.Symbols;

namespace LUAstudio.IntelliSense.Roblox;

public sealed class ApiDumpIngestResult
{
    public FrozenDictionary<string, RobloxClass> Classes { get; init; } = FrozenDictionary<string, RobloxClass>.Empty;
    public FrozenDictionary<string, RobloxMember> Globals { get; init; } = FrozenDictionary<string, RobloxMember>.Empty;
    public FrozenDictionary<string, string> GlobalTypeAliases { get; init; } = FrozenDictionary<string, string>.Empty;
}

public static class ApiDumpIngestor
{
    public static ApiDumpIngestResult Ingest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("Classes", out var classesEl))
        {
            return IngestRbxApiFormat(classesEl);
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            return IngestClassArray(root);
        }

        return BuildBuiltInFallback();
    }

    public static ApiDumpIngestResult BuildBuiltInFallback()
    {
        var instanceMembers = new List<RobloxMember>
        {
            new("Name", SymbolKind.Property, "Instance name.", "string"),
            new("Parent", SymbolKind.Property, "Parent instance.", "Instance?"),
            new("ClassName", SymbolKind.Property, "Class name.", "string"),
            new("FindFirstChild", SymbolKind.Method, "Find child by name.", "Instance?", ["name", "recursive"]),
            new("WaitForChild", SymbolKind.Method, "Wait for child.", "Instance", ["name", "timeOut"]),
            new("GetChildren", SymbolKind.Method, "Get children.", "{Instance}"),
            new("GetDescendants", SymbolKind.Method, "Get descendants.", "{Instance}"),
            new("IsA", SymbolKind.Method, "Check class.", "boolean", ["className"]),
            new("Destroy", SymbolKind.Method, "Destroy instance."),
            new("Clone", SymbolKind.Method, "Clone instance.", "Instance"),
        };

        var dataModelMembers = new List<RobloxMember>
        {
            new("Workspace", SymbolKind.Property, "Workspace service.", "Workspace"),
            new("PlaceId", SymbolKind.Property, "Place id.", "number"),
            new("GameId", SymbolKind.Property, "Game id.", "number"),
            new("GetService", SymbolKind.Method, "Get service by name.", "Instance", ["serviceName"]),
            new("BindToClose", SymbolKind.Method, "Bind close callback."),
        };

        var playersMembers = new List<RobloxMember>
        {
            new("LocalPlayer", SymbolKind.Property, "Local player.", "Player"),
            new("GetPlayerFromCharacter", SymbolKind.Method, "Player from character.", "Player?", ["character"]),
            new("GetPlayers", SymbolKind.Method, "All players.", "{Player}"),
        };

        var playerMembers = new List<RobloxMember>
        {
            new("Character", SymbolKind.Property, "Character model.", "Model?"),
            new("UserId", SymbolKind.Property, "User id.", "number"),
            new("Name", SymbolKind.Property, "Player name.", "string"),
            new("Team", SymbolKind.Property, "Team.", "Team?"),
        };

        var classes = new Dictionary<string, RobloxClass>(StringComparer.Ordinal)
        {
            ["Instance"] = new("Instance", null, "Base instance.", instanceMembers),
            ["DataModel"] = new("DataModel", "Instance", "Game root.", dataModelMembers),
            ["Workspace"] = new("Workspace", "Model", "3D workspace.", instanceMembers),
            ["Model"] = new("Model", "Instance", "Model container.", instanceMembers),
            ["Players"] = new("Players", "Instance", "Player service.", playersMembers),
            ["Player"] = new("Player", "Instance", "Player.", playerMembers),
            ["BasePart"] = new("BasePart", "Instance", "Physical part.", instanceMembers),
            ["Part"] = new("Part", "BasePart", "Part.", instanceMembers),
            ["Script"] = new("Script", "LuaSourceContainer", "Server script.", instanceMembers),
            ["LocalScript"] = new("LocalScript", "LuaSourceContainer", "Local script.", instanceMembers),
            ["ModuleScript"] = new("ModuleScript", "LuaSourceContainer", "Module.", instanceMembers),
            ["ReplicatedStorage"] = new("ReplicatedStorage", "Instance", "Replicated storage.", instanceMembers),
            ["ServerScriptService"] = new("ServerScriptService", "Instance", "Server scripts.", instanceMembers),
            ["RunService"] = new("RunService", "Instance", "Run service.", new[]
            {
                new RobloxMember("Heartbeat", SymbolKind.Property, "Heartbeat event."),
                new RobloxMember("Stepped", SymbolKind.Property, "Stepped event."),
                new RobloxMember("IsClient", SymbolKind.Property, "Is client.", "boolean"),
                new RobloxMember("IsServer", SymbolKind.Property, "Is server.", "boolean"),
            }),
            ["UserInputService"] = new("UserInputService", "Instance", "Input service.", instanceMembers),
            ["TweenService"] = new("TweenService", "Instance", "Tween service.", instanceMembers),
        };

        var globals = new Dictionary<string, RobloxMember>(StringComparer.Ordinal)
        {
            ["game"] = new("game", SymbolKind.Service, "DataModel root.", "DataModel"),
            ["workspace"] = new("workspace", SymbolKind.Service, "Workspace.", "Workspace"),
            ["script"] = new("script", SymbolKind.Class, "Running script.", "LuaSourceContainer"),
        };

        return new ApiDumpIngestResult
        {
            Classes = classes.ToFrozenDictionary(StringComparer.Ordinal),
            Globals = globals.ToFrozenDictionary(StringComparer.Ordinal),
            GlobalTypeAliases = RobloxGlobalTypes.Aliases.ToFrozenDictionary(StringComparer.Ordinal)
        };
    }

    private static ApiDumpIngestResult IngestRbxApiFormat(JsonElement classesEl)
    {
        var classes = new Dictionary<string, RobloxClass>(StringComparer.Ordinal);
        var globals = new Dictionary<string, RobloxMember>(StringComparer.Ordinal);

        foreach (var cls in classesEl.EnumerateArray())
        {
            var name = cls.GetProperty("Name").GetString() ?? "";
            var super = cls.TryGetProperty("Superclass", out var s) ? ReadTypeName(s) : null;
            var members = new List<RobloxMember>();

            if (cls.TryGetProperty("Members", out var membersEl))
            {
                foreach (var m in membersEl.EnumerateArray())
                {
                    var memberName = m.GetProperty("Name").GetString() ?? "";
                    var memberType = m.TryGetProperty("MemberType", out var mt) ? ReadTypeName(mt) : "Property";
                    var kind = MapMemberType(memberType);
                    string? returnType = null;
                    if (m.TryGetProperty("ReturnType", out var rt))
                    {
                        // Current Roblox dumps represent types as objects (for
                        // example { "Name": "string", "Category": "Primitive" }).
                        // Older dumps use a plain string, so support both forms.
                        returnType = ReadTypeName(rt);
                    }

                    members.Add(new RobloxMember(memberName, kind, null, returnType));
                }
            }

            classes[name] = new RobloxClass(name, super, null, members);
        }

        foreach (var (alias, typeName) in RobloxGlobalTypes.Aliases)
        {
            globals[alias] = new RobloxMember(alias, SymbolKind.Service, null, typeName);
        }

        return new ApiDumpIngestResult
        {
            Classes = classes.ToFrozenDictionary(StringComparer.Ordinal),
            Globals = globals.ToFrozenDictionary(StringComparer.Ordinal),
            GlobalTypeAliases = RobloxGlobalTypes.Aliases.ToFrozenDictionary(StringComparer.Ordinal)
        };
    }

    private static ApiDumpIngestResult IngestClassArray(JsonElement array)
    {
        var classes = new Dictionary<string, RobloxClass>(StringComparer.Ordinal);
        foreach (var cls in array.EnumerateArray())
        {
            var name = cls.GetProperty("Name").GetString() ?? "";
            var super = cls.TryGetProperty("Superclass", out var s) ? ReadTypeName(s) : null;
            classes[name] = new RobloxClass(name, super, null, Array.Empty<RobloxMember>());
        }

        return new ApiDumpIngestResult
        {
            Classes = classes.ToFrozenDictionary(StringComparer.Ordinal),
            Globals = RobloxGlobalTypes.Aliases.ToDictionary(
                kv => kv.Key,
                kv => new RobloxMember(kv.Key, SymbolKind.Service, null, kv.Value)).ToFrozenDictionary(StringComparer.Ordinal),
            GlobalTypeAliases = RobloxGlobalTypes.Aliases.ToFrozenDictionary(StringComparer.Ordinal)
        };
    }

    private static SymbolKind MapMemberType(string? memberType) => memberType switch
    {
        "Function" => SymbolKind.Method,
        "Event" => SymbolKind.Method,
        "Property" => SymbolKind.Property,
        _ => SymbolKind.Field
    };

    private static string? ReadTypeName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("Name", out var name) &&
            name.ValueKind == JsonValueKind.String)
        {
            return name.GetString();
        }

        return null;
    }
}
