using System.Text.Json;

namespace LANCommander.Server.Services;

/// <summary>
/// The server's last self-update: which versions it moved between, and why it was undone if it was.
/// Kept beside the update journal until the next update replaces it. The first administrator to open
/// the server UI afterwards is shown it, once; it stays readable from the Updates settings page.
/// </summary>
public sealed class ServerUpdateNotice
{
    public const string FileName = "LastUpdate.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    // Two administrators opening the UI at the same moment must not both claim the notice
    private static readonly object ClaimLock = new();

    public string Version { get; set; } = String.Empty;

    public string? PreviousVersion { get; set; }

    /// <summary>Why the update was undone, or null when it installed.</summary>
    public string? Error { get; set; }

    public DateTimeOffset UpdatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The administrator who was shown the notice, once it has been shown.</summary>
    public string? SeenBy { get; set; }

    public DateTimeOffset? SeenOn { get; set; }

    public bool Succeeded => Error == null;

    public void Save(string journalDirectory)
    {
        Directory.CreateDirectory(journalDirectory);

        File.WriteAllText(Path.Combine(journalDirectory, FileName), JsonSerializer.Serialize(this, SerializerOptions));
    }

    public static ServerUpdateNotice? Load(string journalDirectory)
    {
        var path = Path.Combine(journalDirectory, FileName);

        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<ServerUpdateNotice>(File.ReadAllText(path), SerializerOptions)
                : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the notice for <paramref name="userName"/> to be shown, marking it seen, when no one has
    /// been shown it yet. Returns null when there is no notice or someone already saw it.
    /// </summary>
    public static ServerUpdateNotice? TryClaim(string journalDirectory, string? userName)
    {
        lock (ClaimLock)
        {
            var notice = Load(journalDirectory);

            if (notice == null || notice.SeenOn != null)
                return null;

            notice.SeenBy = userName;
            notice.SeenOn = DateTimeOffset.UtcNow;
            notice.Save(journalDirectory);

            return notice;
        }
    }
}
