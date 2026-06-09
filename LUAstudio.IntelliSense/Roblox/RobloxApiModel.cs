using LUAstudio.IntelliSense.Symbols;

namespace LUAstudio.IntelliSense.Roblox;

public sealed record RobloxClass(
    string Name,
    string? SuperClass,
    string? Documentation,
    IReadOnlyList<RobloxMember> Members);

public sealed record RobloxMember(
    string Name,
    SymbolKind Kind,
    string? Documentation,
    string? ReturnType = null,
    IReadOnlyList<string>? Parameters = null);

public static class RobloxGlobalTypes
{
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["game"] = "DataModel",
        ["workspace"] = "Workspace",
        ["script"] = "LuaSourceContainer",
        ["plugin"] = "Plugin",
        ["shared"] = "SharedTable",
    };

    public static readonly HashSet<string> Services = new(StringComparer.Ordinal)
    {
        "Workspace", "Players", "Lighting", "ReplicatedStorage", "ServerStorage",
        "ServerScriptService", "StarterGui", "StarterPack", "StarterPlayer",
        "SoundService", "Chat", "Teams", "TweenService", "RunService",
        "UserInputService", "CollectionService", "Debris", "PathfindingService",
        "PhysicsService", "MarketplaceService", "ContextActionService", "TextService",
        "HttpService", "DataStoreService", "MessagingService", "TeleportService",
    };
}
