using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.IPC;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// The session must offer to restart elevated whenever an installer escalates out of reach.
/// </summary>
/// <remarks>
/// This is the whole entry point to capturing a self-elevating installer: without the offer the
/// user cannot restart with elevation, the real installer is never instrumented, and nothing is
/// captured at all. It was previously inferred from a failed InjectCommand, which stopped
/// happening once same-architecture processes were no longer routed — so it is now carried on
/// the discovery message and asserted here.
/// </remarks>
public class PackagingElevationDetectionTests
{
    [Fact]
    public async Task OffersElevationWhenAProcessOfTheWorkersOwnArchitectureIsOutOfReach()
    {
        var (session, factory) = Build();

        var reasons = new List<string>();

        session.ElevationRequired += (_, reason) => reasons.Add(reason);

        await session.StartAsync(Options());

        // Exactly what an x86 worker reports for an installer that just escalated.
        factory.Worker(ProcessArchitecture.X86).Report(AccessDenied(52560));

        await WaitForAsync(() => reasons.Count > 0);

        reasons.ShouldHaveSingleItem().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task OffersElevationOnlyOnce()
    {
        // A escalating installer produces one of these per child discovered.
        var (session, factory) = Build();

        var count = 0;

        session.ElevationRequired += (_, _) => Interlocked.Increment(ref count);

        await session.StartAsync(Options());

        var worker = factory.Worker(ProcessArchitecture.X86);

        foreach (var pid in new[] { 52560, 38416, 41000, 41001 })
            worker.Report(AccessDenied(pid));

        await WaitForAsync(() => Volatile.Read(ref count) > 0);
        await Task.Delay(100);

        Volatile.Read(ref count).ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotOfferElevationForAnOrdinaryInjectionFailure()
    {
        var (session, factory) = Build();

        var offered = false;

        session.ElevationRequired += (_, _) => offered = true;

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X86).Report(new ProcessDiscoveredMessage
        {
            ProcessId = 4242,
            Architecture = ProcessArchitecture.X86,
            InjectedLocally = false,
            InjectionError = "The process has already exited.",
            RequiresElevation = false,
        });

        await WaitForAsync(() => session.Snapshot().Processes.Count > 0);
        await Task.Delay(100);

        offered.ShouldBeFalse();
    }

    [Fact]
    public async Task DoesNotOfferElevationOnceAlreadyElevated()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());
        await session.RestartElevatedAsync();

        var offered = false;

        session.ElevationRequired += (_, _) => offered = true;

        factory.Created.Last(w => w.Architecture == ProcessArchitecture.X86 && w.IsElevated)
            .Report(AccessDenied(52560));

        await WaitForAsync(() => session.Snapshot().Processes.Count > 0);
        await Task.Delay(100);

        offered.ShouldBeFalse();
    }

    [Fact]
    public async Task StillRecordsTheProcessAsUninstrumented()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X86).Report(AccessDenied(52560));

        await WaitForAsync(() => session.Snapshot().Processes.Count > 0);

        var entry = session.Snapshot().Processes.ShouldHaveSingleItem();

        entry.Instrumented.ShouldBeFalse();
        entry.InstrumentationError.ShouldNotBeNullOrEmpty();
    }

    private static ProcessDiscoveredMessage AccessDenied(int processId) => new()
    {
        ProcessId = processId,
        Architecture = ProcessArchitecture.X86,
        ImagePath = @"G:\setup_close_combat_2.0.0.1.exe",
        InjectedLocally = false,
        InjectionError = $"OpenProcess failed for PID {processId}.",
        Win32Error = 5,
        RequiresElevation = true,
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
