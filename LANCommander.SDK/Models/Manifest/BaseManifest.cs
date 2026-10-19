using System;
using LANCommander.SDK.Helpers;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Models.Manifest;

public class BaseManifest : BaseModel
{
    /// <summary>
    /// URL of the JSON Schema describing this manifest's root type and version, e.g.
    /// <c>https://docs.lancommander.app/schemas/2.1.16/Game.schema.json</c>. Populated at export time
    /// (see <see cref="ManifestSchemaHelper"/>) so editors/AI tools can validate or author a
    /// Manifest.yml, and so an importer can determine which schema version to validate against.
    /// Serialized first (Order = -100) so it appears at the top of the YAML file.
    /// </summary>
    [YamlMember(Alias = "$schema", Order = -100)]
    public string Schema { get; set; }

    public string ManifestVersion { get; set; }

    public bool IsLegacyManifest() => String.IsNullOrWhiteSpace(ManifestVersion);
}