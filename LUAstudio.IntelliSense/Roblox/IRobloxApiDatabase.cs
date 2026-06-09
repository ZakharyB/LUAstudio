namespace LUAstudio.IntelliSense.Roblox;

public interface IRobloxApiDatabase
{
    IReadOnlyDictionary<string, string> GlobalTypeAliases { get; }

    IReadOnlyList<string> ServiceNames { get; }

    bool TryGetGlobal(string name, out RobloxMember member);

    bool TryGetClass(string className, out RobloxClass service);

    bool TryGetMember(string className, string memberName, out RobloxMember member);

    IReadOnlyList<RobloxMember> GetMembers(string className, bool includeInherited = true);

    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    Task ReloadFromPathAsync(string? path, CancellationToken cancellationToken = default);

    Task DownloadLatestAsync(string? url = null, CancellationToken cancellationToken = default);
}
