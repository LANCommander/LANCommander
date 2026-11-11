using System.ComponentModel.DataAnnotations;

namespace LANCommander.Server.Data.Models;

public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Failed,
    Canceled,
}

public sealed class WebhookDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public WebhookSubscription Subscription { get; set; } = null!;
    public Guid EventId { get; set; }
    [MaxLength(100)]
    public string EventType { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptOn { get; set; } = DateTime.UtcNow;
    public WebhookDeliveryStatus Status { get; set; }
    public int Attempts { get; set; }
    public int? StatusCode { get; set; }
    [MaxLength(100)]
    public string? Error { get; set; }
}
