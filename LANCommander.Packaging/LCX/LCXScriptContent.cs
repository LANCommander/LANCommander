using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Packaging.LCX;

/// <summary>A script and its contents to embed in an LCX package.</summary>
public sealed record LCXScriptContent(Script Manifest, Stream Content);
