using System.Globalization;
using System.Text.Json;
using LANCommander.SDK.Models;
using YamlDotNet.Serialization;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>
/// Renders dotted Interposer option values as the YAML document the DLL merges over its Config.yml
/// (<see cref="LANCommander.Interposer.InterposerService.ConfigOverlay"/>), typed by the schema the
/// way the redistributable's Install script renders Config.yml: booleans and numbers as such, lists,
/// which are stored as JSON, as sequences. A null value is written as a YAML null, which the DLL takes
/// as "remove this key".
/// </summary>
public static class InterposerConfigOverlay
{
    // Quoting what would otherwise read back as something else: an empty string must stay a string,
    // because a bare empty value is a null, and a null removes the key
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithQuotingNecessaryStrings()
        .Build();

    /// <summary>The overlay, or null when there is nothing to layer.</summary>
    public static string? Build(IReadOnlyDictionary<string, string?> values) =>
        Build(values, InterposerOptions.Definitions);

    public static string? Build(IReadOnlyDictionary<string, string?> values, IReadOnlyDictionary<string, OptionDefinition> definitions)
    {
        if (values.Count == 0)
            return null;

        var root = new SortedDictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (key, value) in values)
        {
            var segments = key.Split('.');
            var node = root;

            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!node.TryGetValue(segments[i], out var child) || child is not SortedDictionary<string, object?> map)
                {
                    map = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                    node[segments[i]] = map;
                }

                node = map;
            }

            definitions.TryGetValue(key, out var definition);
            node[segments[^1]] = Convert(value, definition);
        }

        return Serializer.Serialize(root);
    }

    private static object? Convert(string? value, OptionDefinition? definition)
    {
        if (value == null || definition == null)
            return value;

        if (definition.IsList)
            return ConvertList(value, definition);

        return ConvertScalar(value, definition.Type);
    }

    private static object ConvertScalar(string value, string? type)
    {
        switch (type?.ToLowerInvariant())
        {
            case "bool" when bool.TryParse(value, out var boolean):
                return boolean;
            case "int" when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number):
                return number;
            default:
                return value;
        }
    }

    /// <summary>A list value is a JSON array: of scalars, or of objects keyed by the list's fields.</summary>
    private static object ConvertList(string value, OptionDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<object?>();

        JsonElement array;

        try
        {
            using var document = JsonDocument.Parse(value);
            array = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Passed on as text: the DLL ignores a list key that holds no sequence, and says so in its log
            return value;
        }

        if (array.ValueKind != JsonValueKind.Array)
            return value;

        var items = new List<object?>();

        foreach (var item in array.EnumerateArray())
        {
            if (definition.IsCompositeList && item.ValueKind == JsonValueKind.Object)
            {
                var row = new SortedDictionary<string, object?>(StringComparer.Ordinal);

                foreach (var property in item.EnumerateObject())
                {
                    definition.Fields!.TryGetValue(property.Name, out var field);

                    var text = ReadScalar(property.Value);
                    row[property.Name] = text == null ? null : ConvertScalar(text, field?.Type);
                }

                items.Add(row);
            }
            else
            {
                var text = ReadScalar(item);
                items.Add(text == null ? null : ConvertScalar(text, definition.ItemType));
            }
        }

        return items;
    }

    private static string? ReadScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.GetRawText(),
    };
}
