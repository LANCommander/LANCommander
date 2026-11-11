using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LANCommander.SDK.Events;

/// <summary>
/// The envelope LANCommander publishes for every lifecycle event, for example in webhook request
/// bodies. Consumers should ignore unknown properties and event types so additive changes stay
/// compatible.
/// </summary>
/// <param name="Source">
/// Where the action originated: <see cref="ServerEventSources.Server"/> or
/// <see cref="ServerEventSources.Launcher"/>. Always matches the prefix of <paramref name="EventType"/>.
/// </param>
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

/// <summary>The originators an event type can be prefixed with.</summary>
public static class ServerEventSources
{
    /// <summary>The action happened on the server, for example an administrator creating a game.</summary>
    public const string Server = "server";

    /// <summary>The action happened in a launcher and was reported to the server, for example a game install.</summary>
    public const string Launcher = "launcher";
}

/// <summary>
/// Event type keys, formatted as <c>{source}.{resource}.{action}</c>. The source prefix identifies
/// where the action originated; see <see cref="ServerEventSources"/>.
/// </summary>
public static class ServerEventTypes
{
    public const string GameCreated = "server.game.created";
    public const string ArchiveUploaded = "server.game.archive.upload_completed";
    public const string UserLogin = "server.user.login";
    public const string UserLogout = "server.user.logout";
    public const string ServerStarted = "server.game_server.started";
    public const string ServerStopped = "server.game_server.stopped";
    public const string Test = "server.webhook.test";

    public const string GameInstalled = "launcher.game.install.completed";
    public const string GameInstallFailed = "launcher.game.install.failed";
    public const string GameUninstalled = "launcher.game.uninstall.completed";
    public const string PlayStarted = "launcher.game.play.started";
    public const string PlayEnded = "launcher.game.play.ended";
    public const string SaveUploaded = "launcher.game.save.uploaded";
    public const string ToolInstalled = "launcher.tool.installed";
    public const string RedistributableInstalled = "launcher.redistributable.installed";

    public static IReadOnlyList<string> All { get; } =
    [
        GameCreated, ArchiveUploaded, UserLogin, UserLogout, ServerStarted, ServerStopped,
        GameInstalled, GameInstallFailed, GameUninstalled, PlayStarted, PlayEnded, SaveUploaded,
        ToolInstalled, RedistributableInstalled, Test,
    ];

    /// <summary>Returns the <see cref="ServerEventSources"/> value an event type is prefixed with.</summary>
    public static string GetSource(string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        var separator = eventType.IndexOf('.');
        return separator > 0 ? eventType[..separator] : eventType;
    }
}
