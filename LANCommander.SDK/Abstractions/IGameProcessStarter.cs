using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Abstractions;

/// <summary>
/// Starts a game's process in place of a plain <see cref="Process.Start()"/>, for a host that needs to
/// launch games its own way, such as the launcher starting them under the Interposer to join the
/// server's network relay. Optional: where none is registered, or it declines, the SDK starts the
/// process itself.
/// </summary>
public interface IGameProcessStarter
{
    /// <summary>
    /// Starts the game, or returns null to leave it to the SDK. Must not throw for a game it merely
    /// cannot start its way; declining lets the game start normally.
    /// </summary>
    Task<StartedGameProcess> StartAsync(GameProcessStartContext context, CancellationToken cancellationToken);
}

/// <summary>The game about to be started, and how the SDK would start it.</summary>
/// <param name="StartInfo">The start info the SDK built from the action. Already expanded and complete.</param>
/// <param name="GameId">The game being played.</param>
/// <param name="InstallDirectory">Where it is installed.</param>
/// <param name="Manifest">Its manifest.</param>
/// <param name="Action">The action being run.</param>
public sealed record GameProcessStartContext(
    ProcessStartInfo StartInfo,
    Guid GameId,
    string InstallDirectory,
    Models.Manifest.Game Manifest,
    Models.Manifest.Action Action);

/// <summary>
/// A game process an <see cref="IGameProcessStarter"/> started, with whatever has to stay alive while
/// it runs. The SDK disposes it once the game and its child processes have exited.
/// </summary>
public sealed class StartedGameProcess(Process process, IDisposable lifetime = null) : IDisposable
{
    public Process Process { get; } = process ?? throw new ArgumentNullException(nameof(process));

    public void Dispose() => lifetime?.Dispose();
}
