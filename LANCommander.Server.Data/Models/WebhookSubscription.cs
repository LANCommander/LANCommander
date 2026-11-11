using System.ComponentModel.DataAnnotations;

namespace LANCommander.Server.Data.Models;

public sealed class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)]
    public string Name { get; set; } = "";
    [MaxLength(2048)]
    public string Endpoint { get; set; } = "";
    public string ProtectedSecret { get; set; } = "";
    public string EventTypesJson { get; set; } = "[]";
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
}
