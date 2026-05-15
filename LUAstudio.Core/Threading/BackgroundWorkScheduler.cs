namespace LUAstudio.Core.Threading;

public sealed class BackgroundWorkScheduler : IBackgroundWorkScheduler
{
    private readonly SemaphoreSlim _gate = new(initialCount: 4, maxCount: 4);

    public void QueueLongRunning(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        _ = Task.Run(async () =>
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await work(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }, cancellationToken);
    }
}
