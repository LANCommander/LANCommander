using System.Text.Json.Serialization;

namespace LANCommander.Packaging.IPC;

/// <summary>
/// Source-generated serialization for the packaging protocol.
/// <para>
/// Source generation is not an optimization here, it is a requirement: the worker publishes
/// with PublishTrimmed and reflection-based serialization would be trimmed away.
/// </para>
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PackagingMessage))]
public partial class PackagingJsonContext : JsonSerializerContext
{
}
