using System.Text.Json;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Events;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Services.Events;

public sealed class WebhookEdit
{
    public string Name { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string? Secret { get; set; }
    public bool ClearSecret { get; set; }
    public string[] EventTypes { get; set; } = [];
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
}

public sealed record WebhookInformation(Guid Id, string Name, string Endpoint, string[] EventTypes,
    bool Enabled, int TimeoutSeconds, int MaxAttempts, bool HasSigningSecret);

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
            .Select(ToInformation).ToList();
    }

    public async Task<WebhookInformation?> GetAsync(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var subscription = await context.Set<WebhookSubscription>().AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
        return subscription == null ? null : ToInformation(subscription);
    }

    private static WebhookInformation ToInformation(WebhookSubscription subscription) =>
        new(subscription.Id, subscription.Name, subscription.Endpoint,
            JsonSerializer.Deserialize<string[]>(subscription.EventTypesJson)!, subscription.Enabled,
            subscription.TimeoutSeconds, subscription.MaxAttempts, !string.IsNullOrEmpty(subscription.ProtectedSecret));

    public async Task<Guid> SaveAsync(Guid? id, WebhookEdit edit)
    {
        Validate(edit);
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
        if (edit.ClearSecret)
            subscription.ProtectedSecret = "";
        else if (!string.IsNullOrEmpty(edit.Secret))
            subscription.ProtectedSecret = _protector.Protect(edit.Secret);
        if (!id.HasValue)
            context.Add(subscription);
        await context.SaveChangesAsync();
        return subscription.Id;
    }

    internal static void Validate(WebhookEdit edit)
    {
        if (string.IsNullOrWhiteSpace(edit.Name) || edit.Name.Length > 100)
            throw new ArgumentException("A name of at most 100 characters is required.");
        if (!Uri.TryCreate(edit.Endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.Query) || edit.Endpoint.Length > 2048)
            throw new ArgumentException("Use an HTTP(S) URL without credentials, a query string or a fragment.");
        if (edit.ClearSecret && !string.IsNullOrEmpty(edit.Secret))
            throw new ArgumentException("Cannot replace and remove the signing secret at the same time.");
        if (!string.IsNullOrEmpty(edit.Secret) && edit.Secret.Length is not (>= 32 and <= 1024))
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

    public static ServerEvent CreateTestPreview(string eventType, Guid? subscriptionId = null)
    {
        var now = DateTime.UtcNow;
        var targetId = Guid.NewGuid();
        var targetType = "game";
        object payload;
        switch (eventType)
        {
            case ServerEventTypes.GameCreated:
                payload = new { title = "Example game" };
                break;
            case ServerEventTypes.ArchiveUploaded:
                payload = new { archiveId = Guid.NewGuid(), gameVersionId = Guid.NewGuid(), version = "1.0" };
                break;
            case ServerEventTypes.PlayStarted:
                payload = new { sessionId = Guid.NewGuid(), start = now.AddMinutes(-30) };
                break;
            case ServerEventTypes.PlayEnded:
                payload = new { sessionId = Guid.NewGuid(), start = now.AddMinutes(-30), end = now, timedOut = false };
                break;
            case ServerEventTypes.UserLogin:
            case ServerEventTypes.UserLogout:
                targetType = "user";
                payload = new { };
                break;
            case ServerEventTypes.SaveUploaded:
                payload = new { saveId = Guid.NewGuid(), size = 1024L, platforms = SDK.Enums.RuntimePlatform.Windows };
                break;
            case ServerEventTypes.ServerStarted:
            case ServerEventTypes.ServerStopped:
                targetType = "server";
                payload = new { serverName = "Example server" };
                break;
            case ServerEventTypes.GameInstalled:
            case ServerEventTypes.GameInstallFailed:
            case ServerEventTypes.GameUninstalled:
            case ServerEventTypes.ToolInstalled:
            case ServerEventTypes.RedistributableInstalled:
                targetType = eventType == ServerEventTypes.ToolInstalled ? "tool"
                    : eventType == ServerEventTypes.RedistributableInstalled ? "redistributable" : "game";
                payload = new { reportedByLauncher = true };
                break;
            case ServerEventTypes.Test:
                targetType = "webhook";
                targetId = subscriptionId ?? targetId;
                payload = new { message = "LANCommander test webhook" };
                break;
            default:
                throw new ArgumentException("Select a supported event type.", nameof(eventType));
        }

        return new ServerEvent(Guid.NewGuid(), eventType, now, ServerEventTypes.GetSource(eventType), null,
            targetType, targetId, eventType == ServerEventTypes.GameInstallFailed ? "failed" : "completed", null,
            JsonSerializer.SerializeToElement(payload, ServerEventPublisher.JsonOptions));
    }

    public async Task<Guid> TestAsync(Guid id, string eventType = ServerEventTypes.Test)
    {
        var message = CreateTestPreview(eventType, id);
        await using var context = await contextFactory.CreateDbContextAsync();
        var subscription = await context.Set<WebhookSubscription>().FindAsync(id)
            ?? throw new KeyNotFoundException("Webhook subscription not found.");
        if (!subscription.Enabled)
            throw new ArgumentException("Enable the subscription before testing it.");

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

    public async Task<string?> GetDeliveryBodyAsync(Guid subscriptionId, Guid deliveryId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Set<WebhookDelivery>().AsNoTracking()
            .Where(d => d.SubscriptionId == subscriptionId && d.Id == deliveryId)
            .Select(d => d.Body).SingleOrDefaultAsync();
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
