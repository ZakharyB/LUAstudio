namespace LUAstudio.Git;

public interface IGitStatusService : IDisposable
{
    event EventHandler? StatusChanged;

    IReadOnlyList<GitFileStatus> Files { get; }

    IReadOnlyList<GitCommit> Commits { get; }

    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task StageAsync(GitFileStatus file, CancellationToken cancellationToken = default);

    Task UnstageAsync(GitFileStatus file, CancellationToken cancellationToken = default);

    Task CommitAsync(string message, CancellationToken cancellationToken = default);
}
