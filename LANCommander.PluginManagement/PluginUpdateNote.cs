using System.Text;
using System.Text.Json;

namespace LANCommander.PluginManagement;

/// <summary>A plugin that a restart moved to a new version, with the new version's release notes.</summary>
/// <param name="ReleaseNotes">The new package's releaseNotes; null when it has none.</param>
public sealed record PluginUpdateNote(
    string Folder,
    string PackageId,
    string Name,
    string PreviousVersion,
    string Version,
    string? ReleaseNotes)
{
    public const string FileName = "UpdatedPlugins.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    // The server's first administrators may load pages at the same moment; only one takes the notes
    private static readonly object FileLock = new();

    /// <summary>
    /// Adds notes to those waiting in <paramref name="directory"/>. A plugin updated again before its
    /// notes were shown keeps the version it started from and takes the newest notes.
    /// </summary>
    internal static void Add(string directory, IReadOnlyCollection<PluginUpdateNote> notes)
    {
        if (notes.Count == 0)
            return;

        lock (FileLock)
        {
            var merged = Read(directory).ToDictionary(n => n.Folder, StringComparer.OrdinalIgnoreCase);

            foreach (var note in notes)
                merged[note.Folder] = merged.TryGetValue(note.Folder, out var earlier)
                    ? note with { PreviousVersion = earlier.PreviousVersion }
                    : note;

            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, FileName), JsonSerializer.Serialize(merged.Values.ToList(), SerializerOptions));
        }
    }

    /// <summary>Returns the notes waiting in <paramref name="directory"/> and deletes them, so they're shown once.</summary>
    internal static IReadOnlyList<PluginUpdateNote> Take(string directory)
    {
        lock (FileLock)
        {
            var notes = Read(directory);

            if (notes.Count > 0)
                File.Delete(Path.Combine(directory, FileName));

            return notes;
        }
    }

    /// <summary>One document with a section per plugin, for a dialog to render.</summary>
    public static string ToMarkdown(IEnumerable<PluginUpdateNote> notes)
    {
        var builder = new StringBuilder();

        foreach (var note in notes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (builder.Length > 0)
                builder.AppendLine().AppendLine();

            builder.Append("## ").Append(note.Name).Append(' ').AppendLine(note.Version).AppendLine();
            builder.Append("Updated from version ").Append(note.PreviousVersion).AppendLine(".").AppendLine();
            builder.Append(String.IsNullOrWhiteSpace(note.ReleaseNotes) ? "_This version has no release notes._" : note.ReleaseNotes.Trim());
        }

        return builder.ToString();
    }

    private static List<PluginUpdateNote> Read(string directory)
    {
        var path = Path.Combine(directory, FileName);

        if (!File.Exists(path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<PluginUpdateNote>>(File.ReadAllText(path), SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
