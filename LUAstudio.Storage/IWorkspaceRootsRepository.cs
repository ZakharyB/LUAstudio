namespace LUAstudio.Storage;

public interface IWorkspaceRootsRepository
{
    Task<IReadOnlyList<WorkspaceRootRecord>> GetOrderedAsync(CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(IReadOnlyList<string> orderedFullPaths, CancellationToken cancellationToken = default);
}
