using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.IPC;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// Stopping must always complete, however badly a worker is behaving.
/// </summary>
/// <remarks>
/// The monitoring step disables Back, Next and Stop while a capture is running, so a stop that
/// never returns leaves the wizard with no usable control at all. A wedged worker leaves a pipe
/// write blocked on a full buffer with nobody draining it, which is exactly the case that has to
/// stay bounded.
/// </remarks>
public class PackagingTeardownTests
{
    /// <summary>A worker whose sends never complete, like a write to a wedged peer.</summary>
    private sealed class HangingWorker(ProcessArchitecture architecture) : IPackagingWorkerChannel
    {
        public ProcessArchitecture Architecture { get; } = architecture;

        public bool IsElevated => false;

        public bool IsConnected => true;

        public bool WasDisposed { get; private set; }

#pragma warning disable CS0067 // Never raised by this stub.
        public event EventHandler<PackagingMessage>? MessageReceived;
        public event EventHandler<string>? Faulted;
#pragma warning restore CS0067

        public Task SendAsync(PackagingMessage message, CancellationToken cancellationToken = default)
        {
            // Completes only when the caller's own token fires; an unbounded caller waits forever.
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            WasDisposed = true;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class HangingWorkerFactory : IPackagingWorkerFactory
    {
        public bool IsSupported => true;

        public IReadOnlyList<ProcessArchitecture> SupportedArchitectures =>
            [ProcessArchitecture.X64];

        public List<HangingWorker> Created { get; } = [];

        public Task<IReadOnlyList<IPackagingWorkerChannel>> StartWorkersAsync(
            Guid sessionId, bool elevated, CancellationToken cancellationToken)
        {
            var worker = new HangingWorker(ProcessArchitecture.X64);

            Created.Add(worker);

            return Task.FromResult<IReadOnlyList<IPackagingWorkerChannel>>([worker]);
        }
    }

    [Fact]
    public async Task StoppingCompletesEvenWhenAWorkerNeverAcknowledges()
    {
        var factory = new HangingWorkerFactory();
        var session = new PackagingSessionService(
            factory, NullLogger<PackagingSessionService>.Instance);

        // StartAsync also sends to the hanging worker, so it has to be bounded too.
        await session.StartAsync(new PackagingSessionOptions
        {
            InstallerPath = @"G:\setup_example.exe",
        }).WaitAsync(TimeSpan.FromSeconds(20));

        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(20));

        session.State.ShouldBe(PackagingSessionState.Stopped);
    }

    [Fact]
    public async Task AnUnresponsiveWorkerIsStillDisposed()
    {
        // Disposal is what closes the pipe, and closing the pipe is how the worker process
        // learns to exit. Skipping it leaves the worker running after the install is over.
        var factory = new HangingWorkerFactory();
        var session = new PackagingSessionService(
            factory, NullLogger<PackagingSessionService>.Instance);

        await session.StartAsync(new PackagingSessionOptions
        {
            InstallerPath = @"G:\setup_example.exe",
        }).WaitAsync(TimeSpan.FromSeconds(20));

        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(20));

        factory.Created.ShouldAllBe(w => w.WasDisposed);
    }
}
