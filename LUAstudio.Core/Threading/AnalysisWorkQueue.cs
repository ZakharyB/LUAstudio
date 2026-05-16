using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LUAstudio.Core.Threading;

/// <summary>
/// Per-document analysis queue using <see cref="Channel{T}"/> for low-allocation scheduling.
/// Cancels in-flight work when a newer request arrives for the same document.
/// </summary>
public sealed class AnalysisWorkQueue : IAnalysisWorkQueue, IDisposable
{
    private readonly ConcurrentDictionary<string, DocumentQueue> _queues = new(StringComparer.Ordinal);
    private readonly IBackgroundWorkScheduler _scheduler;

    public AnalysisWorkQueue(IBackgroundWorkScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public ValueTask EnqueueAsync(
        string documentKey,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentKey);
        ArgumentNullException.ThrowIfNull(work);

        var queue = _queues.GetOrAdd(documentKey, _ => new DocumentQueue(_scheduler));
        return queue.EnqueueAsync(work, cancellationToken);
    }

    public void CancelPending(string documentKey)
    {
        if (_queues.TryGetValue(documentKey, out var queue))
        {
            queue.CancelCurrent();
        }
    }

    public void Dispose()
    {
        foreach (var queue in _queues.Values)
        {
            queue.Dispose();
        }

        _queues.Clear();
    }

    private sealed class DocumentQueue : IDisposable
    {
        private readonly Channel<WorkItem> _channel;
        private readonly IBackgroundWorkScheduler _scheduler;
        private CancellationTokenSource? _currentCts;
        private readonly object _ctsLock = new();

        public DocumentQueue(IBackgroundWorkScheduler scheduler)
        {
            _scheduler = scheduler;
            _channel = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            _scheduler.QueueLongRunning(ProcessLoopAsync);
        }

        public ValueTask EnqueueAsync(Func<CancellationToken, Task> work, CancellationToken outerToken)
        {
            var item = new WorkItem(work, outerToken);
            return _channel.Writer.WriteAsync(item);
        }

        public void CancelCurrent()
        {
            lock (_ctsLock)
            {
                _currentCts?.Cancel();
            }
        }

        private async Task ProcessLoopAsync(CancellationToken hostToken)
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(hostToken).ConfigureAwait(false))
            {
                CancellationTokenSource linked;
                lock (_ctsLock)
                {
                    _currentCts?.Cancel();
                    _currentCts?.Dispose();
                    _currentCts = CancellationTokenSource.CreateLinkedTokenSource(hostToken, item.OuterToken);
                    linked = _currentCts;
                }

                try
                {
                    await item.Work(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (linked.Token.IsCancellationRequested)
                {
                    // Superseded by a newer edit — expected.
                }
            }
        }

        public void Dispose()
        {
            _channel.Writer.TryComplete();
            lock (_ctsLock)
            {
                _currentCts?.Cancel();
                _currentCts?.Dispose();
                _currentCts = null;
            }
        }

        private readonly record struct WorkItem(Func<CancellationToken, Task> Work, CancellationToken OuterToken);
    }
}
