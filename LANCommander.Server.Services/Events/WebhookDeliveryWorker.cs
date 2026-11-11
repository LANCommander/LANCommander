using System.Collections.Concurrent;
using System.Text.Json;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Events;
using LANCommander.Server.Plugins.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.ServiceProvider;

namespace LANCommander.Server.Services.Events;

public sealed record DeliverWebhook(Guid DeliveryId);

public sealed class WebhookDeliveryWorker(
    IDbContextFactory<DatabaseContext> contextFactory,
    IBusRegistry buses,
    ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    // Another instance on a shared transport may handle a scheduled delivery, so this instance's
    // reservation is never completed. Expire it; the database claim prevents duplicate sends.
    private static readonly TimeSpan ReservationTimeout = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<Guid, DateTime> _scheduled = new();

    internal void Complete(Guid id) => _scheduled.TryRemove(id, out _);

    private bool TryReserve(Guid id, DateTime now)
    {
        var expires = now.Add(ReservationTimeout);
        return _scheduled.TryAdd(id, expires)
            || (_scheduled.TryGetValue(id, out var current) && current <= now && _scheduled.TryUpdate(id, expires, current));
    }

    internal async Task ScheduleAsync(Guid? eventId = null, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var query = context.Set<WebhookDelivery>().AsNoTracking()
            .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptOn <= now);
        if (eventId.HasValue)
            query = query.Where(d => d.EventId == eventId.Value);

        foreach (var id in await query.OrderBy(d => d.NextAttemptOn).Select(d => d.Id).Take(100).ToListAsync(cancellationToken))
        {
            if (!TryReserve(id, now))
                continue;
            try
            {
                await buses.GetBus(EventServiceCollectionExtensions.BusName).SendLocal(new DeliverWebhook(id));
            }
            catch
            {
                Complete(id);
                throw;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                await ScheduleAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Could not schedule pending webhook deliveries; failure type {FailureType}", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Records a delivery for each enabled subscription before the event is published.</summary>
public sealed class WebhookEventOutbox : IServerEventOutbox
{
    public async Task AddAsync(DatabaseContext context, ServerEvent serverEvent, string body, CancellationToken cancellationToken)
    {
        var subscriptions = await context.Set<WebhookSubscription>().AsNoTracking()
            .Where(s => s.Enabled).ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            if (!JsonSerializer.Deserialize<string[]>(subscription.EventTypesJson)!.Contains(serverEvent.EventType))
                continue;

            context.Add(new WebhookDelivery
            {
                SubscriptionId = subscription.Id,
                EventId = serverEvent.EventId,
                EventType = serverEvent.EventType,
                Body = body,
            });
        }
    }
}

/// <summary>The built-in webhook consumer: schedules the deliveries recorded by the outbox.</summary>
public sealed class WebhookEventConsumer(WebhookDeliveryWorker worker) : IServerEventConsumer
{
    public Task HandleAsync(ServerEvent serverEvent, CancellationToken cancellationToken) =>
        worker.ScheduleAsync(serverEvent.EventId, cancellationToken);
}