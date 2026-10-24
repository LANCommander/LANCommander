using System.Text.Json;

namespace LANCommander.Server.Plugins.Installation;

/// <summary>
/// The package a plugin folder was installed from, kept beside the plugin's files. A folder without
/// one was copied in by hand.
/// </summary>
public sealed class PluginPackageRecord
{
    public const string FileName = ".package.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Description { get; set; }
    public string? ProjectUrl { get; set; }
    public string? IconUrl { get; set; }

    /// <summary>The source it came from; both null for an uploaded package.</summary>
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }

    public DateTime InstalledOn { get; set; }

    /// <summary>Installed from nuget.org as an official plugin; see <see cref="OfficialPlugins"/>.</summary>
    public bool IsOfficial { get; set; }

    public static PluginPackageRecord? Read(string directory)
    {
        var path = Path.Combine(directory, FileName);

        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PluginPackageRecord>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Write(string directory) =>
        File.WriteAllText(Path.Combine(directory, FileName), JsonSerializer.Serialize(this, SerializerOptions));
}
