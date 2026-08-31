using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.IPC;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// Covers what happens to the already-running installer when a capture is restarted elevated.
/// </summary>
/// <remarks>
/// The failure this guards against: the installer self-elevates, injection into it fails with
/// access denied, the user clicks "Restart as administrator", and the original is still running
/// — so the second instance aborts with "another setup is already running".
/// </remarks>
public class PackagingRestartElevatedTests
{
    private const int RootPid = 4100;
    private const int ElevatedPid = 4200;

    [Fact]
    public async Task TerminatesTheProcessInjectionFailedOn()
    {
        // The self-elevated installer is never successfully injected into, so it must still be
        // reachable for termination. It is the whole point of the restart.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        ReportInstallerThatSelfElevated(factory);

        await session.RestartElevatedAsync();

        var terminate = ElevatedWorker(factory).SentOfType<TerminateProcessesCommand>()
            .ShouldHaveSingleItem();

        terminate.ProcessIds.ShouldContain(ElevatedPid);
    }

    [Fact]
    public async Task TerminatesTheInstallerItLaunchedItself()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        ReportInstallerThatSelfElevated(factory);

        await session.RestartElevatedAsync();

        ElevatedWorker(factory).SentOfType<TerminateProcessesCommand>()
            .ShouldHaveSingleItem()
            .ProcessIds.ShouldContain(RootPid);
    }

    [Fact]
    public async Task AsksTheElevatedWorkerToTerminateRatherThanTheOldOne()
    {
        // A worker running at the launcher's integrity level cannot kill a process that
        // escalated above it, so the request has to go to the new elevated worker.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        var originalWorkers = factory.Created.ToList();

        ReportInstallerThatSelfElevated(factory);

        await session.RestartElevatedAsync();

        foreach (var worker in originalWorkers)
        {
            worker.SentOfType<TerminateProcessesCommand>().ShouldBeEmpty();
            worker.SentOfType<StopCommand>().ShouldHaveSingleItem().TerminateTargets.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task TerminatesBeforeRelaunching()
    {
        // Ordering is the fix: the worker handles messages sequentially and awaits each, so a
        // terminate queued ahead of the launch has completed before the installer restarts.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        ReportInstallerThatSelfElevated(factory);

        await session.RestartElevatedAsync();

        var sent = ElevatedWorker(factory).Sent.ToList();

        var terminateIndex = sent.FindIndex(m => m is TerminateProcessesCommand);
        var launchIndex = sent.FindIndex(m => m is LaunchInstallerCommand);

        terminateIndex.ShouldBeGreaterThanOrEqualTo(0);
        launchIndex.ShouldBeGreaterThanOrEqualTo(0);
        terminateIndex.ShouldBeLessThan(launchIndex);
    }

    [Fact]
    public async Task DoesNotTerminateProcessesThatAlreadyExited()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        ReportInstallerThatSelfElevated(factory);

        factory.Worker(ProcessArchitecture.X64).Report(
            new ProcessExitedMessage { ProcessId = RootPid, IsRoot = true });

        await WaitForAsync(() => session.Snapshot().Processes.Any(p => p.ProcessId == RootPid && p.HasExited));

        await session.RestartElevatedAsync();

        var terminate = ElevatedWorker(factory).SentOfType<TerminateProcessesCommand>()
            .ShouldHaveSingleItem();

        terminate.ProcessIds.ShouldNotContain(RootPid);
        terminate.ProcessIds.ShouldContain(ElevatedPid);
    }

    [Fact]
    public async Task SendsNoTerminateWhenNothingIsLeftRunning()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        await session.RestartElevatedAsync();

        ElevatedWorker(factory).SentOfType<TerminateProcessesCommand>().ShouldBeEmpty();
    }

    [Fact]
    public async Task AnOrdinaryStopLeavesTheInstallerAlone()
    {
        // The user may be mid-install; killing their installer is worse than losing the tail
        // of a capture.
        var (session, factory) = Build();

        await session.StartAsync(Options());
        await session.StopAsync();

        foreach (var worker in factory.Created)
        {
            worker.SentOfType<StopCommand>()
                .ShouldHaveSingleItem()
                .TerminateTargets.ShouldBeFalse();
        }
    }

    /// <summary>
    /// Mimics the reported sequence: the worker launches the installer, the installer relaunches
    /// itself elevated, and injection into that second process is denied.
    /// </summary>
    private static void ReportInstallerThatSelfElevated(FakePackagingWorkerFactory factory)
    {
        var worker = factory.Worker(ProcessArchitecture.X64);

        worker.Report(new ProcessDiscoveredMessage
        {
            ProcessId = RootPid,
            Architecture = ProcessArchitecture.X64,
            ImagePath = @"G:\setup_example.exe",
            InjectedLocally = true,
        });

        worker.Report(new ProcessDiscoveredMessage
        {
            ProcessId = ElevatedPid,
            ParentProcessId = RootPid,
            Architecture = ProcessArchitecture.X64,
            ImagePath = @"C:\Users\Example\AppData\Local\Temp\is-ABCDE.tmp\setup.tmp",
            InjectedLocally = false,
            InjectionError = "Access is denied",
        });
    }

    private static FakePackagingWorker ElevatedWorker(FakePackagingWorkerFactory factory) =>
        factory.Created.Last(w => w.Architecture == ProcessArchitecture.X64 && w.IsElevated);

    private static (PackagingSessionService Session, FakePackagingWorkerFactory Factory) Build()
    {
        var factory = new FakePackagingWorkerFactory(
            ProcessArchitecture.X64, ProcessArchitecture.X86);

        return (new PackagingSessionService(factory, NullLogger<PackagingSessionService>.Instance), factory);
    }

    private static PackagingSessionOptions Options() =>
        new() { InstallerPath = @"G:\setup_example.exe" };

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
