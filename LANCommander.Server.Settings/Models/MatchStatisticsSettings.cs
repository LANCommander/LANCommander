namespace LANCommander.Server.Settings.Models;

/// <summary>How the server collects multiplayer match events, results and attachments.</summary>
public class MatchStatisticsSettings
{
    /// <summary>Accept match reports. When off, the reporting endpoints answer 404 and plugins can't record.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Events held in memory waiting to be written. When the queue is full, reporters get 429 and retry,
    /// so this bounds memory use rather than throughput.
    /// </summary>
    public int QueueCapacity { get; set; } = 50_000;

    /// <summary>The most events written to the database in one transaction.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>How long, in milliseconds, the writer waits to fill a batch before writing what it has.</summary>
    public int FlushInterval { get; set; } = 250;

    /// <summary>The most events accepted in one request.</summary>
    public int MaxEventsPerRequest { get; set; } = 500;

    /// <summary>The largest payload accepted for one event, in kilobytes.</summary>
    public int MaxPayloadSize { get; set; } = 16;

    /// <summary>Minutes an open match may go without events before it's marked abandoned. Zero never abandons.</summary>
    public int AbandonAfter { get; set; } = 30;

    /// <summary>Largest attachment, such as a demo, in megabytes.</summary>
    public int MaxAttachmentSize { get; set; } = 2048;

    /// <summary>Days attachments are kept. Zero keeps them.</summary>
    public int AttachmentRetention { get; set; } = 0;

    /// <summary>Hours an attachment upload may stay incomplete before it's removed.</summary>
    public int PendingAttachmentTimeout { get; set; } = 24;
}
