using System;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Models.Manifest;

public class BaseManifest : BaseModel
{
    public string ManifestVersion { get; set; }

    public bool IsLegacyManifest() => String.IsNullOrWhiteSpace(ManifestVersion);
}