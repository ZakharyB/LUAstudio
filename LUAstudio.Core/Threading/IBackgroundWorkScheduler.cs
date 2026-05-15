namespace LUAstudio.Core.Threading;

public interface IBackgroundWorkScheduler
{
    void QueueLongRunning(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}
