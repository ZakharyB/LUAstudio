using System.Collections.Concurrent;

namespace LUAstudio.Core.Events;

public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<T>(Action<T> handler) where T : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        var key = typeof(T);
        _handlers.AddOrUpdate(key, _ => [handler], (_, list) =>
        {
            lock (list)
            {
                list.Add(handler);
                return list;
            }
        });
    }

    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryGetValue(typeof(T), out var list))
        {
            return;
        }

        lock (list)
        {
            list.RemoveAll(d => ReferenceEquals(d, handler));
        }
    }

    public void Publish<T>(T message) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_handlers.TryGetValue(typeof(T), out var list))
        {
            return;
        }

        Action<T>[] snapshot;
        lock (list)
        {
            snapshot = list.OfType<Action<T>>().ToArray();
        }

        foreach (var handler in snapshot)
        {
            handler(message);
        }
    }
}
