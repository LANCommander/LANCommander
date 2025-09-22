using System;
using System.Collections.Generic;
using LANCommander.SDK.Abstractions;

namespace LANCommander.SDK.Configuration;

public class LANCommanderConfiguration : ILANCommanderConfiguration
{
    public Uri BaseAddress { get; set; }
    public bool OfflineMode { get; set; }
    public bool DebugScripts { get; set; }
    public int BeaconPort { get; set; }
    public long UploadChunkSize { get; set; }
    public IEnumerable<string> InstallDirectories { get; set; }
    public int IPXRelayPort { get; set; }
    public string IPXRelayHost { get; set; }
}