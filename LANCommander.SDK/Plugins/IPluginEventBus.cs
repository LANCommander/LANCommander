using System;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// A minimal in-process, strongly-typed event aggregator that lets plugins react to host lifecycle
/// events (game install/launch/uninstall, login, etc.). Registered as a singleton in both hosts.
/// </summary>
public interface IPluginEventBus
{
    /// <summary>
    /// Subscribes a handler to events of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <returns>A token that unsubscribes the handler when disposed.</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler);

    /// <summary>
    /// Publishes an event to all subscribed handlers. Each handler is awaited and isolated so a
    /// throwing handler cannot break the publisher or other handlers.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}
