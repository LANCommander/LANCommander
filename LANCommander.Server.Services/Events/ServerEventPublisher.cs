using System.Security.Claims;
using System.Text.Json;
using LANCommander.Server.Data;
using LANCommander.Server.Plugins.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rebus.ServiceProvider;

namespace LANCommander.Server.Services.Events;

public sealed class ServerEventPublisher(
    IDbContextFactory<DatabaseContext> contextFactory,
    IHttpContextAccessor httpContextAccessor,
    IBusRegistry buses,
    IEnumerable<IServerEventOutbox> outboxes,
    ILogger<ServerEventPublisher> logger) : IServerEventPublisher
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(string eventType, string targetType, Guid targetId, object payload,
        Guid? actorId = null, Guid? correlationId = null, string status = "completed")
    {
        if (!ServerEventTypes.All.Contains(eventType))
            throw new ArgumentException("Unknown server event type.", nameof(eventType));

        actorId ??= Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId : null;

        var message = new ServerEvent(Guid.NewGuid(), eventType, DateTime.UtcNow, "LANCommander.Server",
            actorId, targetType, targetId, status, correlationId, JsonSerializer.SerializeToElement(payload, JsonOptions));

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var body = JsonSerializer.Serialize(message, JsonOptions);
            foreach (var outbox in outboxes)
                await outbox.AddAsync(context, message, body, CancellationToken.None);

            // Persist before handing off to the transport. Recovery redispatches this work if the
            // process exits before the event is consumed.
            await context.SaveChangesAsync();
            if (buses.TryGetBus(EventServiceCollectionExtensions.BusName, out var bus))
                await bus.Publish(message);
        }
        catch (Exception ex)
        {
            // Notifications must not turn a successfully completed operation into a failure.
            // Never log exception messages: HTTP/database exceptions may contain credentials.
            logger.LogError("Could not publish server event {EventId} ({EventType}); failure type {FailureType}",
                message.EventId, eventType, ex.GetType().Name);
        }
    }
}