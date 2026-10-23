using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests;

/// <summary>
/// An <see cref="IGameProcessStarter"/> may start a game its own way; when it declines or fails, the
/// game starts as it always has. Windows only: the actions run cmd.exe.
/// </summary>
public class GameProcessStarterTests : IDisposable
{
    private readonly string _installDirectory = Directory.CreateTempSubdirectory("starter").FullName;
    private readonly ManifestGame _manifest = new() { Id = Guid.NewGuid(), Title = "Test Game" };

    public void Dispose() => Directory.Delete(_installDirectory, recursive: true);

    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    /// <summary>An action that leaves a file behind, proving it was run directly.</summary>
    private ManifestAction MarkerAction(out string marker)
    {
        marker = Path.Combine(_installDirectory, "ran-directly.txt");

        return new ManifestAction { Name = "Play", Path = Cmd, Arguments = $"/c echo ran> \"{marker}\"", IsPrimaryAction = true };
    }

    [Fact]
    public async Task AStartersProcessIsTheGameAndOutlivesItsLifetimeOnlyUntilItExits()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await ManifestHelper.WriteAsync(_manifest, _installDirectory);

        var starter = new StartingStarter();
        using var context = new ProcessExecutionContext(NullLogger<ProcessExecutionContext>.Instance) { GameProcessStarter = starter };

        await context.ExecuteGameActionAsync(_installDirectory, _manifest.Id, MarkerAction(out var marker));

        // The starter saw the action as the SDK would have run it
        Assert.Equal(Cmd, starter.Seen!.StartInfo.FileName);
        Assert.Equal(_manifest.Id, starter.Seen.GameId);
        Assert.Equal("Test Game", starter.Seen.Manifest.Title);

        // Its process, not one the SDK started, was waited on, and only then was its lifetime ended
        Assert.Same(starter.Started, context.GetProcess());
        Assert.True(starter.Started!.HasExited);
        Assert.True(starter.Lifetime.DisposedAfterExit);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task AStarterThatDeclinesLeavesTheGameToStartDirectly()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await ManifestHelper.WriteAsync(_manifest, _installDirectory);

        using var context = new ProcessExecutionContext(NullLogger<ProcessExecutionContext>.Instance) { GameProcessStarter = new DecliningStarter() };

        await context.ExecuteGameActionAsync(_installDirectory, _manifest.Id, MarkerAction(out var marker));

        Assert.True(File.Exists(marker));
    }

    [Fact]
    public async Task AStarterThatFailsCannotStopTheGameFromStarting()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await ManifestHelper.WriteAsync(_manifest, _installDirectory);

        using var context = new ProcessExecutionContext(NullLogger<ProcessExecutionContext>.Instance) { GameProcessStarter = new FailingStarter() };

        await context.ExecuteGameActionAsync(_installDirectory, _manifest.Id, MarkerAction(out var marker));

        Assert.True(File.Exists(marker));
    }

    private sealed class StartingStarter : IGameProcessStarter
    {
        public GameProcessStartContext? Seen { get; private set; }

        public Process? Started { get; private set; }

        public TrackingLifetime Lifetime { get; } = new();

        public Task<StartedGameProcess> StartAsync(GameProcessStartContext context, CancellationToken cancellationToken)
        {
            Seen = context;

            // Something other than the action, running for a moment
            Started = Process.Start(new ProcessStartInfo(Cmd, "/c ping -n 2 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true })!;
            Lifetime.Process = Started;

            return Task.FromResult(new StartedGameProcess(Started, Lifetime));
        }
    }

    private sealed class TrackingLifetime : IDisposable
    {
        public Process? Process { get; set; }

        public bool DisposedAfterExit { get; private set; }

        public void Dispose() => DisposedAfterExit = Process?.HasExited == true;
    }

    private sealed class DecliningStarter : IGameProcessStarter
    {
        public Task<StartedGameProcess> StartAsync(GameProcessStartContext context, CancellationToken cancellationToken)
            => Task.FromResult<StartedGameProcess>(null!);
    }

    private sealed class FailingStarter : IGameProcessStarter
    {
        public Task<StartedGameProcess> StartAsync(GameProcessStartContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The starter is broken");
    }
}
