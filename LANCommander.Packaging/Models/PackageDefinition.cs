using System.IO.Compression;
using LANCommander.Packaging.Changes;
using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Packaging.Models;

/// <summary>
/// Everything the wizard has gathered about a package in progress. Carried across every step
/// and consumed by <see cref="Lcx.LcxBuilder"/>.
/// </summary>
public class PackageDefinition
{
    /// <summary>The installer that was monitored.</summary>
    public string InstallerPath { get; set; } = string.Empty;

    /// <summary>Root directory the game was installed into; archive paths are relative to it.</summary>
    public string InstallDirectory { get; set; } = string.Empty;

    /// <summary>Everything the capture saw, before the user narrowed it down.</summary>
    public List<FileChange> FileChanges { get; set; } = [];

    public List<RegistryChange> RegistryChanges { get; set; } = [];

    /// <summary>Absolute paths of the files the user chose to include.</summary>
    public List<string> SelectedFiles { get; set; } = [];

    public List<RegistryChange> SelectedRegistryEntries { get; set; } = [];

    /// <summary>Manifest metadata, populated by the metadata step.</summary>
    public Game Manifest { get; set; } = new();

    /// <summary>Where to write the .lcx, when saving to disk.</summary>
    public string OutputPath { get; set; } = string.Empty;

    public bool PatchGameSpy { get; set; }

    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
}
