using System.Text.Json;

namespace LANCommander.SDK.Helpers;

internal static class SdkJsonOptions
{
    /// <summary>
    /// The server's minimal APIs serialize with <see cref="JsonSerializerDefaults.Web"/>, which emits
    /// camelCase. Deserializing those payloads into our PascalCase models requires case-insensitive
    /// matching, otherwise every property silently binds to null.
    /// </summary>
    internal static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
