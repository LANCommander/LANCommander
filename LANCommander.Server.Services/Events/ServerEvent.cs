using LANCommander.Server.Data;
using LANCommander.Server.Plugins.Messaging;

namespace LANCommander.Server.Services.Events;

public interface IServerEventPublisher
{
    Task PublishAsync(string eventType, string targetType, Guid targetId, object payload,
        Guid? actorId = null, Guid? correlationId = null, string status = "completed");
}

/// <summary>
/// Durable work for built-in consumers, saved before the event is handed to the transport so it
/// survives a restart while the in-memory transport is in use.
/// </summary>
public interface IServerEventOutbox
{
    Task AddAsync(DatabaseContext context, ServerEvent serverEvent, string body, CancellationToken cancellationToken);
}