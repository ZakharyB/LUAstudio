using LUAstudio.Storage;

namespace LUAstudio.Workspace;

public interface IRecentFilesService
{
    Task RecordFileOpenedAsync(string fullPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRecentFilesAsync(int maxCount = 10, CancellationToken cancellationToken = default);
}

public sealed class RecentFilesService : IRecentFilesService
{
    private readonly IRecentFilesRepository _repository;

    public RecentFilesService(IRecentFilesRepository repository)
    {
        _repository = repository;
    }

    public Task RecordFileOpenedAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var normalized = Path.GetFullPath(fullPath);
        return _repository.RecordOpenAsync(normalized, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetRecentFilesAsync(int maxCount = 10, CancellationToken cancellationToken = default) =>
        _repository.GetRecentAsync(maxCount, cancellationToken);
}
