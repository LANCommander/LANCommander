using LANCommander.Server.Plugins.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Transport.InMem;
using Rebus.Config;
using Rebus.Subscriptions;
using Rebus.Transport;

namespace LANCommander.Server.Services.Events;

/// <summary>Fans each server event out to every registered <see cref="IServerEventConsumer"/>.</summary>
public sealed class ServerEventDispatcher(
    IEnumerable<IServerEventConsumer> consumers,
    IHostApplicationLifetime lifetime,
    ILogger<ServerEventDispatcher> logger) : IHandleMessages<ServerEvent>
{
    public Task Handle(ServerEvent message) =>
        Task.WhenAll(consumers.Select(consumer => DispatchAsync(consumer, message, lifetime.ApplicationStopping)));

    private async Task DispatchAsync(IServerEventConsumer consumer, ServerEvent message, CancellationToken cancellationToken)
    {
        try
        {
            await consumer.HandleAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Consumers own their retries. Log only the failure type: plugin exceptions may
            // contain credentials or remote content.
            logger.LogError("Server event consumer {Consumer} failed for event {EventId} ({EventType}); failure type {FailureType}",
                consumer.GetType().FullName, message.EventId, message.EventType, ex.GetType().Name);
        }
    }
}

internal sealed class InMemoryServerEventTransport : IServerEventTransport
{
    private readonly InMemNetwork _network = new();

    public void ConfigureTransport(StandardConfigurer<ITransport> transport, string queueName) =>
        transport.UseInMemoryTransport(_network, queueName);

    public void ConfigureSubscriptions(StandardConfigurer<ISubscriptionStorage> subscriptions) { }
}
