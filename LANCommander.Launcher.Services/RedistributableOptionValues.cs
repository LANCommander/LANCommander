using System.Text.Json;

namespace LANCommander.Launcher.Services;

/// <summary>
/// The player's redistributable option values as the launcher stores them in
/// <see cref="Data.Models.Game.RedistributableOptions"/>: redistributable id → dotted option key → value.
/// A null value takes the key out of the config, where the option is armed by its key's presence alone.
/// </summary>
public static class RedistributableOptionValues
{
    public static Dictionary<Guid, Dictionary<string, string?>> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<Guid, Dictionary<string, string?>>>(json) ?? new();
        }
        catch (JsonException)
        {
            // A value the launcher cannot read is one the player has to set again, not a failed launch
            return new();
        }
    }

    /// <summary>The values for one redistributable; empty when the player has set none.</summary>
    public static Dictionary<string, string?> For(string? json, Guid redistributableId) =>
        Parse(json).TryGetValue(redistributableId, out var values) && values != null ? values : new();

    /// <summary>Stores <paramref name="values"/> for one redistributable, dropping it when empty.</summary>
    public static string? With(string? json, Guid redistributableId, IReadOnlyDictionary<string, string?> values)
    {
        var all = Parse(json);

        if (values.Count == 0)
            all.Remove(redistributableId);
        else
            all[redistributableId] = new Dictionary<string, string?>(values);

        return all.Count == 0 ? null : JsonSerializer.Serialize(all);
    }
}
