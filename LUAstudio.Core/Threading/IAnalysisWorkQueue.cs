namespace LUAstudio.Core.Threading;

/// <summary>
/// Serializes background analysis work per document with cancellation of stale requests.
/// </summary>
public interface IAnalysisWorkQueue
{
    ValueTask EnqueueAsync(
        string documentKey,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    void CancelPending(string documentKey);
}
