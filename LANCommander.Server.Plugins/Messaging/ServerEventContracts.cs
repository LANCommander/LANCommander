using LANCommander.SDK.Events;
using Rebus.Config;
using Rebus.Subscriptions;
using Rebus.Transport;

namespace LANCommander.Server.Plugins.Messaging;

/// <summary>
/// Receives every published <see cref="ServerEvent"/>, for example to forward it to Slack.
/// Register an implementation in <c>IPlugin.ConfigureServices</c>. Consumers run in the background
/// after the originating operation and are isolated from each other. Consumers own their own
/// filtering, retries and persistence: an exception is logged and the event is dropped for that
/// consumer.
/// </summary>
public interface IServerEventConsumer
{
    Task HandleAsync(ServerEvent serverEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Replaces the default in-memory transport that carries server events, for example with
/// RabbitMQ. Register one implementation in <c>IPlugin.ConfigureServices</c>; when several are
/// registered, the first is used. Every server instance sharing the transport consumes from the
/// same queue, so each event is handled by one instance.
/// </summary>
public interface IServerEventTransport
{
    /// <summary>Configures the Rebus transport, consuming from <paramref name="queueName"/>.</summary>
    void ConfigureTransport(StandardConfigurer<ITransport> transport, string queueName);

    /// <summary>
    /// Configures subscription storage for transports without native publish/subscribe support.
    /// Brokers such as RabbitMQ do not need this.
    /// </summary>
    void ConfigureSubscriptions(StandardConfigurer<ISubscriptionStorage> subscriptions) { }
}
