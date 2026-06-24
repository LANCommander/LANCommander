using System.IO.Compression;
using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Packager.Models;

public class PackageContext
{
    public string InstallerPath { get; set; } = string.Empty;
    public string InstallDirectory { get; set; } = string.Empty;
    public List<FileChangeEntry> FileChanges { get; set; } = new();
    public List<RegistryChangeEntry> RegistryChanges { get; set; } = new();
    public List<string> SelectedFiles { get; set; } = new();
    public List<RegistryChangeEntry> SelectedRegistryEntries { get; set; } = new();
    public Game Manifest { get; set; } = new();
    public string OutputPath { get; set; } = string.Empty;

    // Options
    public bool PatchGameSpy { get; set; }
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
    public bool WriteSummaryLog { get; set; }
}
