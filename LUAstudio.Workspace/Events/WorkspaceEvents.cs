namespace LUAstudio.Workspace.Events;

public sealed record WorkspaceRootsChangedEvent;

public sealed record WorkspaceFileSystemChangedEvent(string RootPath, IReadOnlyList<string> AffectedPaths);
