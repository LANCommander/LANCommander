namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Reads and writes the system master output volume. Implementations are platform-specific
/// and selected in DI.
/// </summary>
public interface IVolumeService
{
    /// <summary>Whether volume control is available on this system (e.g. the required tool exists).</summary>
    bool IsSupported { get; }

    /// <summary>Current master output volume, 0-100. Returns 0 when unsupported.</summary>
    int GetVolume();

    /// <summary>Sets the master output volume. <paramref name="percent"/> is clamped to 0-100.</summary>
    void SetVolume(int percent);

    /// <summary>Whether the master output is muted.</summary>
    bool GetMuted();
}
