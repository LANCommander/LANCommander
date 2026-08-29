namespace LANCommander.Launcher.Services.Packaging;

/// <summary>
/// Stand-in used where packaging cannot run — non-Windows hosts, or a build with no worker
/// binaries deployed.
/// </summary>
/// <remarks>
/// Registering this rather than leaving the service unregistered keeps platform checks out of
/// every view model: they bind to <see cref="IsSupported"/> and the packaging entry point
/// simply never appears.
/// </remarks>
public class UnsupportedPackagingSessionService : IPackagingSessionService
{
    private const string Message =
        "Packaging is only available on Windows, and requires the packaging workers to be installed " +
        "alongside the launcher.";

    public bool IsSupported => false;

    public PackagingSessionState State => PackagingSessionState.Idle;

#pragma warning disable CS0067 // Never raised; packaging cannot run on this host.
    public event EventHandler<PackagingCounters>? CountersChanged;
    public event EventHandler<string>? Logged;
    public event EventHandler? InstallerExited;
    public event EventHandler<string>? ElevationRequired;
#pragma warning restore CS0067

    public Task StartAsync(PackagingSessionOptions options, CancellationToken cancellationToken = default) =>
        throw new PlatformNotSupportedException(Message);

    public Task RestartElevatedAsync(CancellationToken cancellationToken = default) =>
        throw new PlatformNotSupportedException(Message);

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public PackagingSessionSnapshot Snapshot() => new();

    public void Reset()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
