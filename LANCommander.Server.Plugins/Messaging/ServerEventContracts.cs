using System.Text.Json;
using Rebus.Config;
using Rebus.Subscriptions;
using Rebus.Transport;

namespace LANCommander.Server.Plugins.Messaging;

/// <summary>
/// The envelope LANCommander publishes for every server lifecycle event. Consumers should
/// ignore unknown properties and event types so additive changes stay compatible.
/// </summary>
public sealed record ServerEvent(
    Guid EventId,
    string EventType,
    DateTime OccurredAt,
    string Source,
    Guid? ActorId,
    string TargetType,
    Guid TargetId,
    string Status,
    Guid? CorrelationId,
    JsonElement Payload);

public static class ServerEventTypes
{
    public const string GameCreated = "game.created";
    public const string ArchiveUploaded = "game.archive.upload_completed";
    public const string GameInstalled = "game.install.completed";
    public const string GameInstallFailed = "game.install.failed";
    public const string GameUninstalled = "game.uninstall.completed";
    public const string PlayStarted = "game.play.started";
    public const string PlayEnded = "game.play.ended";
    public const string UserLogin = "user.login";
    public const string UserLogout = "user.logout";
    public const string SaveUploaded = "game.save.uploaded";
    public const string ServerStarted = "server.started";
    public const string ServerStopped = "server.stopped";
    public const string ToolInstalled = "tool.installed";
    public const string RedistributableInstalled = "redistributable.installed";
    public const string Test = "webhook.test";

    public static IReadOnlyList<string> All { get; } =
    [
        GameCreated, ArchiveUploaded, GameInstalled, GameInstallFailed, GameUninstalled,
        PlayStarted, PlayEnded, UserLogin, UserLogout, SaveUploaded, ServerStarted,
        ServerStopped, ToolInstalled, RedistributableInstalled, Test,
    ];
}

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
