using LUAstudio.IntelliSense.Symbols;

namespace LUAstudio.IntelliSense.Roblox;

public interface IRobloxApiDatabase
{
    bool TryGetGlobal(string name, out RobloxMember member);

    bool TryGetService(string serviceName, out RobloxClass service);

    bool TryGetMember(string className, string memberName, out RobloxMember member);

    IReadOnlyList<RobloxMember> GetMembers(string className);

    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
}

public sealed record RobloxClass(string Name, string? Documentation, IReadOnlyList<RobloxMember> Members);

public sealed record RobloxMember(
    string Name,
    SymbolKind Kind,
    string? Documentation,
    string? ReturnType = null,
    IReadOnlyList<RobloxMember>? Children = null);
