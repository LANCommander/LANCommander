using System.Text.Json;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// Match payloads, settings and metadata are kept as JSON text, so they work the same on every
/// database. These convert between that text and the <see cref="JsonElement"/>s the API exposes.
/// </summary>
internal static class MatchJson
{
    /// <summary>The raw JSON of an object, or null for null, undefined or JSON null.</summary>
    public static string? ToText(JsonElement? element)
    {
        if (element is not { } value)
            return null;

        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        return value.GetRawText();
    }

    /// <summary>Whether the element is absent or a JSON object.</summary>
    public static bool IsObjectOrEmpty(JsonElement? element) =>
        element is not { } value
        || value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Object;

    public static JsonElement? ToElement(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        try
        {
            using var document = JsonDocument.Parse(text);

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Stored text is only ever written from parsed JSON, so this means hand-edited data
            return null;
        }
    }
}
