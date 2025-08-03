using System;
using System.Collections.Generic;

namespace LANCommander.SDK.Services;

public class GameInstallationArchiveInfo
{
    public GameManifest Manifest { get; internal set; }
    public List<ArchiveEntry> Entries { get; private set; } = [];
    public List<SavePathEntry> SavePaths { get; internal set; }
}

public class GameInstallationArchiveEntries
{
    public GameInstallationArchiveInfo BaseGame { get; internal set; } = new();
    public Dictionary<Guid, GameInstallationArchiveInfo> DependentGames { get; private set; } = [];
}
