using System.Threading.Tasks;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Describes how to re-launch the launcher as a minimal, elevated process that runs a single script
/// with the supplied runtime parameters and then exits.
/// </summary>
public class ElevatedProcessRequest
{
    /// <summary>The launcher executable to invoke elevated.</summary>
    public required string FileName { get; init; }

    /// <summary>The formatted command line (RunScript verb + options) passed to the elevated process.</summary>
    public required string Arguments { get; init; }

    /// <summary>The working directory the elevated script should run in.</summary>
    public string? WorkingDirectory { get; init; }
}

/// <summary>
/// Launches an elevated process and waits for it to finish. Abstracted so the interceptor's
/// wait-for-completion behavior can be tested without spawning a real UAC-elevated process.
/// </summary>
public interface IElevatedProcessLauncher
{
    /// <summary>
    /// Starts the elevated process described by <paramref name="request"/> and completes only once
    /// that process has exited.
    /// </summary>
    Task LaunchAndWaitAsync(ElevatedProcessRequest request);
}
