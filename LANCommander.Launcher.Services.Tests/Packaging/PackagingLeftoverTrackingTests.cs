using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.IPC;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// Covers the ledger's view of which processes are still alive.
/// </summary>
/// <remarks>
/// The monitoring step decides whether an install has finished by asking whether anything it
/// discovered is still running. A process that was killed but never reported as exited keeps
/// that answer wrong forever, so the capture never ends on its own.
/// </remarks>
public class PackagingLeftoverTrackingTests
{
    [Fact]
    public async Task ATerminatedProcessIsNoLongerCountedAsRunning()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        var worker = factory.Worker(ProcessArchitecture.X86);

        worker.Report(Discovered(2880, injected: true));
        worker.Report(Discovered(13648, injected: true));

        await WaitForAsync(() => session.Snapshot().Processes.Count == 2);

        session.Snapshot().Processes.Count(p => !p.HasExited).ShouldBe(2);

        // What the elevated worker reports back after killing the previous run's leftovers.
        worker.Report(new ProcessExitedMessage { ProcessId = 2880 });
        worker.Report(new ProcessExitedMessage { ProcessId = 13648 });

        await WaitForAsync(() => session.Snapshot().Processes.All(p => p.HasExited));

        session.Snapshot().Processes.Count(p => !p.HasExited).ShouldBe(0);
    }

    [Fact]
    public async Task DoesNotRouteAnInjectionBackToTheWorkerThatFailedIt()
    {
        // Both processes here are x86 and the reporting worker is the x86 one, so the only
        // candidate is the worker that already failed. Retrying would fail identically and just
        // produce a misleading "routing to..." line in the log.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        var worker = factory.Worker(ProcessArchitecture.X86);

        worker.Report(new ProcessDiscoveredMessage
        {
            ProcessId = 52560,
            Architecture = ProcessArchitecture.X86,
            ImagePath = @"G:\setup.exe",
            InjectedLocally = false,
            InjectionError = "OpenProcess failed for PID 52560.",
        });

        await WaitForAsync(() => session.Snapshot().Processes.Count == 1);
        await Task.Delay(100);

        worker.SentOfType<InjectCommand>().ShouldBeEmpty();

        var entry = session.Snapshot().Processes.ShouldHaveSingleItem();

        entry.Instrumented.ShouldBeFalse();
        entry.InstrumentationError.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task StillRoutesAcrossArchitectures()
    {
        // The cross-architecture handoff must survive the change above.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(new ProcessDiscoveredMessage
        {
            ProcessId = 4242,
            Architecture = ProcessArchitecture.X86,
            InjectedLocally = false,
        });

        await WaitForAsync(() => factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>().Count > 0);

        factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>()
            .ShouldHaveSingleItem().ProcessId.ShouldBe(4242);
    }

    private static ProcessDiscoveredMessage Discovered(int processId, bool injected) => new()
    {
        ProcessId = processId,
        Architecture = ProcessArchitecture.X86,
        ImagePath = @"G:\setup_close_combat_2.0.0.1.exe",
        InjectedLocally = injected,
    };

    private static (PackagingSessionService Session, FakePackagingWorkerFactory Factory) Build()
    {
        var factory = new FakePackagingWorkerFactory(
            ProcessArchitecture.X64, ProcessArchitecture.X86);

        return (new PackagingSessionService(factory, NullLogger<PackagingSessionService>.Instance), factory);
    }

    private static PackagingSessionOptions Options() =>
        new() { InstallerPath = @"G:\setup_close_combat_2.0.0.1.exe" };

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        condition().ShouldBeTrue("Timed out waiting for the expected session state.");
    }
}
