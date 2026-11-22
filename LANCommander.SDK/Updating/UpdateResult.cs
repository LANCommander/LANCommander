#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace LANCommander.SDK.Updating;

/// <summary>
/// Why an update was undone. The process that rolled it back writes this beside the journal before
/// starting the previous version again, which reads it, tells the user, and deletes it.
/// </summary>
public sealed class UpdateResult
{
    public const string FileName = "Result.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>The version that failed to install.</summary>
    public string Version { get; set; } = String.Empty;

    /// <summary>The version that is still installed.</summary>
    public string? PreviousVersion { get; set; }

    /// <summary>What went wrong, written for the user.</summary>
    public string Error { get; set; } = String.Empty;

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public void Save(string journalDirectory)
    {
        Directory.CreateDirectory(journalDirectory);

        File.WriteAllText(Path.Combine(journalDirectory, FileName), JsonSerializer.Serialize(this, SerializerOptions));
    }

    /// <summary>Reads and deletes the result in <paramref name="journalDirectory"/>, or returns null when there is none.</summary>
    public static UpdateResult? Take(string journalDirectory)
    {
        var path = Path.Combine(journalDirectory, FileName);

        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<UpdateResult>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Shown again next time; harmless
            }
        }
    }
}
