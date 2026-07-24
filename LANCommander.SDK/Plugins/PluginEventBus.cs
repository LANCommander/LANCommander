using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Plugins;

/// <inheritdoc cref="IPluginEventBus"/>
public sealed class PluginEventBus : IPluginEventBus
{
    private readonly ILogger<PluginEventBus> _logger;
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private readonly object _lock = new();

    public PluginEventBus(ILogger<PluginEventBus> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var list = _handlers.GetOrAdd(typeof(TEvent), _ => new List<object>());

        lock (_lock)
            list.Add(handler);

        return new Subscription(() =>
        {
            lock (_lock)
                list.Remove(handler);
        });
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            return;

        object[] snapshot;
        lock (_lock)
            snapshot = list.ToArray();

        foreach (var entry in snapshot)
        {
            var handler = (Func<TEvent, CancellationToken, Task>)entry;

            try
            {
                await handler(@event, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A plugin handler for {EventType} threw an exception", typeof(TEvent).Name);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
