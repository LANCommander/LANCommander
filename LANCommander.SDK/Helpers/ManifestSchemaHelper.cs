using LANCommander.SDK.Models.Manifest;

namespace LANCommander.SDK.Helpers;

/// <summary>
/// Builds the versioned JSON Schema URL embedded in a manifest's <see cref="BaseManifest.Schema"/>
/// field. Matches the layout published to docs.lancommander.app by the release workflow, which
/// generates one schema per manifest root type (see LANCommander.SchemaGenerator).
/// </summary>
public static class ManifestSchemaHelper
{
    public const string BaseUrl = "https://docs.lancommander.app/schemas";

    public static string GetSchemaUrl(string manifestTypeName, string version)
        => $"{BaseUrl}/{version}/{manifestTypeName}.schema.json";

    public static string GetSchemaUrl<T>(string version) where T : BaseManifest
        => GetSchemaUrl(typeof(T).Name, version);
}
