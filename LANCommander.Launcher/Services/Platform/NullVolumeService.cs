namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Fallback volume service used on unrecognized platforms or when no mixer tool is available.
/// </summary>
public sealed class NullVolumeService : IVolumeService
{
    public bool IsSupported => false;
    public int GetVolume() => 0;
    public void SetVolume(int percent) { }
    public bool GetMuted() => false;
}
