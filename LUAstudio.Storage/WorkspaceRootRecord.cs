namespace LUAstudio.Storage;

public sealed record WorkspaceRootRecord(int Id, string Path, int SortOrder, DateTimeOffset AddedUtc);
