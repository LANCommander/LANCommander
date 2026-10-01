using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Packaging.LCX;

/// <summary>An already-normalized ZIP archive to embed in an LCX package.</summary>
public sealed record LCXArchiveContent(Archive Manifest, Stream Content);
