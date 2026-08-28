namespace LUAstudio.Git;

public enum GitFileState
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted,
}

public sealed record GitFileStatus(
    string RepositoryRoot,
    string RelativePath,
    string FullPath,
    GitFileState State,
    bool IsStaged)
{
    public string Glyph => State switch
    {
        GitFileState.Added => "A",
        GitFileState.Untracked => "?",
        GitFileState.Conflicted => "U",
        GitFileState.Deleted => "D",
        GitFileState.Renamed => "R",
        _ => "M",
    };
}

public sealed record GitCommit(string Sha, string Subject, string Author, DateTimeOffset AuthoredAt);
