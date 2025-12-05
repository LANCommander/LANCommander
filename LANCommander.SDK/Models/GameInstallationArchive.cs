using System;
using System.Collections.Generic;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

public class GameInstallationArchiveInfo
{
    public Models.Manifest.Game Manifest { get; internal set; }
    public List<ArchiveEntry> Entries { get; private set; } = [];
    public List<SavePathEntry> SavePaths { get; internal set; }
}

public class GameInstallationArchiveEntries
{
    public GameInstallationArchiveInfo BaseGame { get; internal set; } = new();
    public Dictionary<Guid, GameInstallationArchiveInfo> Addons { get; private set; } = [];
}
