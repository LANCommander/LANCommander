using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace LANCommander.Server.Services.Events;

public sealed class WebhookDeliveryHandler(
    IDbContextFactory<DatabaseContext> contextFactory,
    IDataProtectionProvider protectionProvider,
    IHttpClientFactory httpClientFactory,
    IHostApplicationLifetime lifetime,
    WebhookDeliveryWorker worker,
    ILogger<WebhookDeliveryHandler> logger) : IHandleMessages<DeliverWebhook>
{
    public const string HttpClientName = "OutboundWebhooks";

    public async Task Handle(DeliverWebhook message)
    {
        try
        {
            await DeliverAsync(message.DeliveryId, lifetime.ApplicationStopping);
        }
        finally
        {
            worker.Complete(message.DeliveryId);
        }
    }

    internal async Task DeliverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var delivery = await context.Set<WebhookDelivery>().AsNoTracking().Include(d => d.Subscription)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (delivery == null || delivery.Status != WebhookDeliveryStatus.Pending || delivery.NextAttemptOn > DateTime.UtcNow)
            return;

        var subscription = delivery.Subscription;
        var observedAttempts = delivery.Attempts;
        var now = DateTime.UtcNow;
        // Every transition out of this state is conditional on it being unchanged, so only one
        // handler, on any instance sharing the database, can claim an attempt.
        var claimable = context.Set<WebhookDelivery>().Where(d => d.Id == id
            && d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptOn <= now && d.Attempts == observedAttempts);

        if (!subscription.Enabled)
        {
            await claimable.ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, WebhookDeliveryStatus.Canceled), cancellationToken);
            return;
        }

        if (observedAttempts >= subscription.MaxAttempts)
        {
            await claimable.ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, WebhookDeliveryStatus.Failed)
                .SetProperty(d => d.Error, "Attempt limit reached after interrupted delivery"), cancellationToken);
            return;
        }

        // A crash during HTTP leaves this pending, leased until the timeout passes.
        var leaseExpires = now.AddSeconds(subscription.TimeoutSeconds + 5);
        var claimed = await claimable.ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Attempts, observedAttempts + 1)
            .SetProperty(d => d.StatusCode, (int?)null)
            .SetProperty(d => d.Error, (string?)null)
            .SetProperty(d => d.NextAttemptOn, leaseExpires), cancellationToken);
        if (claimed != 1)
            return;

        // Sync the loaded copy with the claim; it is not tracked.
        delivery.Attempts = observedAttempts + 1;
        delivery.StatusCode = null;
        delivery.Error = null;
        delivery.NextAttemptOn = leaseExpires;

        try
        {
            var secret = protectionProvider.CreateProtector("LANCommander.Webhooks.Secrets.v1")
                .Unprotect(subscription.ProtectedSecret);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Endpoint);
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(delivery.Body));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Headers.Add("X-LANCommander-Event-Id", delivery.EventId.ToString());
            request.Headers.Add("X-LANCommander-Delivery-Id", delivery.Id.ToString());
            request.Headers.Add("X-LANCommander-Timestamp", timestamp);
            request.Headers.Add("X-LANCommander-Signature", Sign(secret, timestamp, delivery.Body));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(subscription.TimeoutSeconds));
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            delivery.StatusCode = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                delivery.Status = WebhookDeliveryStatus.Delivered;
            else
                delivery.Error = $"HTTP {(int)response.StatusCode}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or CryptographicException)
        {
            // A receiver can echo secrets in its body, reason phrase or error. Store only a
            // bounded classification, never remote content or the exception's message.
            delivery.Error = ex switch
            {
                CryptographicException => "Signing key unavailable",
                OperationCanceledException => "Delivery timed out",
                _ => "Connection failed",
            };
        }

        if (delivery.Status != WebhookDeliveryStatus.Delivered)
        {
            delivery.Status = delivery.Attempts >= subscription.MaxAttempts
                ? WebhookDeliveryStatus.Failed : WebhookDeliveryStatus.Pending;
            delivery.NextAttemptOn = DateTime.UtcNow.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, delivery.Attempts - 1)));
            logger.LogWarning("Webhook delivery {DeliveryId} to subscription {SubscriptionId}: attempt {Attempt}, status {Status}, error {Error}",
                delivery.Id, subscription.Id, delivery.Attempts, delivery.Status, delivery.Error);
        }

        // Only write the outcome while this handler still owns the claimed attempt.
        await context.Set<WebhookDelivery>()
            .Where(d => d.Id == id && d.Status == WebhookDeliveryStatus.Pending && d.Attempts == delivery.Attempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, delivery.Status)
                .SetProperty(d => d.StatusCode, delivery.StatusCode)
                .SetProperty(d => d.Error, delivery.Error)
                .SetProperty(d => d.NextAttemptOn, delivery.NextAttemptOn), cancellationToken);
    }

    internal static string Sign(string secret, string timestamp, string body) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + body)));
}
