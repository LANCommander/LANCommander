using System.Text.Json;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Plugins.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Services.Events;

public sealed class WebhookEdit
{
    public string Name { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string? Secret { get; set; }
    public string[] EventTypes { get; set; } = [];
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
}

public sealed record WebhookInformation(Guid Id, string Name, string Endpoint, string[] EventTypes,
    bool Enabled, int TimeoutSeconds, int MaxAttempts);

public sealed record WebhookDeliveryInformation(Guid Id, Guid EventId, string EventType, DateTime CreatedOn,
    DateTime NextAttemptOn, WebhookDeliveryStatus Status, int Attempts, int? StatusCode, string? Error);

public sealed class WebhookService(
    IDbContextFactory<DatabaseContext> contextFactory,
    IDataProtectionProvider protectionProvider)
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("LANCommander.Webhooks.Secrets.v1");

    public async Task<IReadOnlyList<WebhookInformation>> GetAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return (await context.Set<WebhookSubscription>().AsNoTracking().OrderBy(s => s.Name).ToListAsync())
            .Select(s => new WebhookInformation(s.Id, s.Name, s.Endpoint,
                JsonSerializer.Deserialize<string[]>(s.EventTypesJson)!, s.Enabled, s.TimeoutSeconds, s.MaxAttempts)).ToList();
    }

    public async Task<Guid> SaveAsync(Guid? id, WebhookEdit edit)
    {
        Validate(edit, id == null);
        await using var context = await contextFactory.CreateDbContextAsync();
        var subscription = id.HasValue
            ? await context.Set<WebhookSubscription>().FindAsync(id.Value)
                ?? throw new KeyNotFoundException("Webhook subscription not found.")
            : new WebhookSubscription();

        subscription.Name = edit.Name.Trim();
        subscription.Endpoint = edit.Endpoint;
        subscription.Enabled = edit.Enabled;
        subscription.TimeoutSeconds = edit.TimeoutSeconds;
        subscription.MaxAttempts = edit.MaxAttempts;
        subscription.EventTypesJson = JsonSerializer.Serialize(edit.EventTypes.Distinct().ToArray());
        if (!string.IsNullOrEmpty(edit.Secret))
            subscription.ProtectedSecret = _protector.Protect(edit.Secret);
        if (!id.HasValue)
            context.Add(subscription);
        await context.SaveChangesAsync();
        return subscription.Id;
    }

    internal static void Validate(WebhookEdit edit, bool creating)
    {
        if (string.IsNullOrWhiteSpace(edit.Name) || edit.Name.Length > 100)
            throw new ArgumentException("A name of at most 100 characters is required.");
        if (!Uri.TryCreate(edit.Endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.Query) || edit.Endpoint.Length > 2048)
            throw new ArgumentException("Use an HTTP(S) URL without credentials, a query string or a fragment.");
        if ((creating || !string.IsNullOrEmpty(edit.Secret)) && (edit.Secret?.Length is not (>= 32 and <= 1024)))
            throw new ArgumentException("The signing secret must contain 32 to 1024 characters.");
        if (edit.TimeoutSeconds is < 1 or > 30 || edit.MaxAttempts is < 1 or > 10)
            throw new ArgumentException("Timeout must be 1-30 seconds and attempts must be 1-10.");
        if (edit.EventTypes == null || edit.EventTypes.Length == 0 || edit.EventTypes.Any(t => !ServerEventTypes.All.Contains(t)))
            throw new ArgumentException("Select at least one supported event type.");
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var subscription = await context.Set<WebhookSubscription>().FindAsync(id);
        if (subscription == null)
            return false;
        context.Remove(subscription);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<Guid> TestAsync(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var subscription = await context.Set<WebhookSubscription>().FindAsync(id)
            ?? throw new KeyNotFoundException("Webhook subscription not found.");
        if (!subscription.Enabled)
            throw new ArgumentException("Enable the subscription before testing it.");

        var message = new ServerEvent(Guid.NewGuid(), ServerEventTypes.Test, DateTime.UtcNow,
            "LANCommander.Server", null, "webhook", id, "completed", null,
            JsonSerializer.SerializeToElement(new { message = "LANCommander test webhook" }));
        var delivery = new WebhookDelivery
        {
            SubscriptionId = id,
            EventId = message.EventId,
            EventType = message.EventType,
            Body = JsonSerializer.Serialize(message, ServerEventPublisher.JsonOptions),
        };
        context.Add(delivery);
        await context.SaveChangesAsync();
        return delivery.Id;
    }

    public async Task<IReadOnlyList<WebhookDeliveryInformation>> GetDeliveriesAsync(Guid subscriptionId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Set<WebhookDelivery>().AsNoTracking()
            .Where(d => d.SubscriptionId == subscriptionId).OrderByDescending(d => d.CreatedOn).Take(100)
            .Select(d => new WebhookDeliveryInformation(d.Id, d.EventId, d.EventType, d.CreatedOn,
                d.NextAttemptOn, d.Status, d.Attempts, d.StatusCode, d.Error)).ToListAsync();
    }
}
