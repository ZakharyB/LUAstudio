namespace LUAstudio.Storage;

public interface IRecentFilesRepository
{
    Task RecordOpenAsync(string fullPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRecentAsync(int maxCount, CancellationToken cancellationToken = default);
}
