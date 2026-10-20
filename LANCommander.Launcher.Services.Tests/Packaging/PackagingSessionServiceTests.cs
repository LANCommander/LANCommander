using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.Changes;
using LANCommander.Packaging.IPC;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// Covers the parts of a capture session that are easy to get wrong and impossible to observe
/// in a manual test: cross-architecture routing, deduplication across workers, and how the
/// session degrades when a worker dies or the installer needs elevation.
/// </summary>
public class PackagingSessionServiceTests
{
    private const string InstallerPath = @"C:\Installers\setup.exe";

    [Fact]
    public async Task StartsEveryWorkerUpFront()
    {
        // Both architectures start before the installer does. Spawning the second one lazily
        // would put a process launch and a handshake on the critical path at the exact moment a
        // short-lived child needs instrumenting.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Created.Count.ShouldBe(2);
        factory.Created.Select(w => w.Architecture).ShouldBe(
            [ProcessArchitecture.X64, ProcessArchitecture.X86], ignoreOrder: true);
    }

    [Fact]
    public async Task PushesCaptureFilterToEveryWorker()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        foreach (var worker in factory.Created)
            worker.SentOfType<SetFilterCommand>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task LaunchesTheInstallerOnExactlyOneWorker()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Created.SelectMany(w => w.SentOfType<LaunchInstallerCommand>())
            .ShouldHaveSingleItem()
            .ExecutablePath.ShouldBe(InstallerPath);
    }

    [Fact]
    public async Task RoutesAnX86ChildDiscoveredByTheX64WorkerToTheX86Worker()
    {
        // The core of the cross-architecture handoff: an x64 worker cannot inject into a 32-bit
        // child, so it reports it and the launcher hands it to the worker that can.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(new ProcessDiscoveredMessage
        {
            ProcessId = 4242,
            ParentProcessId = 100,
            Architecture = ProcessArchitecture.X86,
            InjectedLocally = false,
        });

        await WaitForAsync(() => factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>().Count > 0);

        factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>()
            .ShouldHaveSingleItem()
            .ProcessId.ShouldBe(4242);

        factory.Worker(ProcessArchitecture.X64).SentOfType<InjectCommand>().ShouldBeEmpty();
    }

    [Fact]
    public async Task DoesNotRouteAProcessAWorkerAlreadyInjectedInto()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(new ProcessDiscoveredMessage
        {
            ProcessId = 4242,
            Architecture = ProcessArchitecture.X64,
            InjectedLocally = true,
        });

        await Task.Delay(100);

        factory.Created.SelectMany(w => w.SentOfType<InjectCommand>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RoutesAProcessOnlyOnceEvenWhenBothWorkersReportIt()
    {
        // Both workers poll the same subtree, so the same child is routinely reported twice.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        var discovered = new ProcessDiscoveredMessage
        {
            ProcessId = 4242,
            Architecture = ProcessArchitecture.X86,
            InjectedLocally = false,
        };

        factory.Worker(ProcessArchitecture.X64).Report(discovered);
        factory.Worker(ProcessArchitecture.X86).Report(discovered);

        await WaitForAsync(() => factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>().Count > 0);
        await Task.Delay(100);

        factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>().Count.ShouldBe(1);
    }

    [Fact]
    public async Task RecordsAProcessNoWorkerCanInstrument()
    {
        // ARM64 has no Interposer build. That must be visible, not silently dropped.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(new ProcessDiscoveredMessage
        {
            ProcessId = 4242,
            Architecture = ProcessArchitecture.Arm64,
            InjectedLocally = false,
        });

        await WaitForAsync(() => session.Snapshot().Processes.Count > 0);

        var entry = session.Snapshot().Processes.ShouldHaveSingleItem();

        entry.Instrumented.ShouldBeFalse();
        entry.InstrumentationError.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task MergesChangesFromEveryWorker()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(Batch(@"C:\Games\Example\game.exe"));
        factory.Worker(ProcessArchitecture.X86).Report(Batch(@"C:\Games\Example\data.dat"));

        var files = session.Snapshot().Files;

        files.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeduplicatesAFileSeenByBothWorkers()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(Batch(@"C:\Games\Example\game.exe"));
        factory.Worker(ProcessArchitecture.X86).Report(Batch(@"C:\Games\Example\game.exe"));

        session.Snapshot().Files.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task KeepsTheSameRegistryKeyCapturedFromDifferentArchitectures()
    {
        // Same key path from a 32-bit and a 64-bit process lands in two different physical
        // places under WOW64, so both are needed to generate correct scripts.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(new ChangeBatchMessage
        {
            Registry =
            [
                Registry(ProcessArchitecture.X64),
                Registry(ProcessArchitecture.X86),
            ],
        });

        session.Snapshot().Registry.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeduplicatesTheSameRegistryValueFromTheSameArchitecture()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(new ChangeBatchMessage
        {
            Registry = [Registry(ProcessArchitecture.X86), Registry(ProcessArchitecture.X86)],
        });

        session.Snapshot().Registry.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AccumulatesDroppedEventCounts()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(new ChangeBatchMessage { DroppedCount = 10 });
        factory.Worker(ProcessArchitecture.X86).Report(new ChangeBatchMessage { DroppedCount = 5 });

        session.Snapshot().DroppedEventCount.ShouldBe(15);
    }

    [Fact]
    public async Task RaisesInstallerExitedOnlyForTheRootProcess()
    {
        var (session, factory) = Build();

        var exited = 0;

        session.InstallerExited += (_, _) => Interlocked.Increment(ref exited);

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(
            new ProcessExitedMessage { ProcessId = 1, IsRoot = false });
        factory.Worker(ProcessArchitecture.X64).Report(
            new ProcessExitedMessage { ProcessId = 2, IsRoot = true });

        await WaitForAsync(() => Volatile.Read(ref exited) > 0);

        Volatile.Read(ref exited).ShouldBe(1);
    }

    [Fact]
    public async Task AsksForElevationOnceWhenAWorkerReportsItIsNeeded()
    {
        // A failing installer produces a stream of access-denied results; the user should be
        // asked to elevate exactly once.
        var (session, factory) = Build();

        var requests = 0;

        session.ElevationRequired += (_, _) => Interlocked.Increment(ref requests);

        await session.StartAsync(Options());

        for (var i = 0; i < 5; i++)
        {
            factory.Worker(ProcessArchitecture.X64).Report(new CommandResultMessage
            {
                Success = false,
                RequiresElevation = true,
                Error = "Access is denied",
            });
        }

        await WaitForAsync(() => Volatile.Read(ref requests) > 0);
        await Task.Delay(100);

        Volatile.Read(ref requests).ShouldBe(1);
    }

    [Fact]
    public async Task RestartingElevatedStartsNewWorkersAndKeepsWhatWasCaptured()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(Batch(@"C:\Games\Example\game.exe"));

        await session.RestartElevatedAsync();

        factory.ElevationRequests.ShouldBe([false, true]);
        factory.Created.Count.ShouldBe(4);

        // Changes captured before escalation belong to the same install and are kept.
        session.Snapshot().Files.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RoutesToTheRestartedWorkersAfterElevation()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());
        await session.RestartElevatedAsync();

        factory.Worker(ProcessArchitecture.X64).Report(new ProcessDiscoveredMessage
        {
            ProcessId = 777,
            Architecture = ProcessArchitecture.X86,
            InjectedLocally = false,
        });

        await WaitForAsync(() => factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>().Count > 0);

        factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>()
            .ShouldHaveSingleItem().ProcessId.ShouldBe(777);
    }

    [Fact]
    public async Task ADeadWorkerDoesNotEndTheSession()
    {
        // Partial capture beats none: whatever the surviving workers watch keeps recording.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X86).Fault("crashed");

        factory.Worker(ProcessArchitecture.X64).Report(Batch(@"C:\Games\Example\game.exe"));

        session.State.ShouldBe(PackagingSessionState.Monitoring);
        session.Snapshot().Files.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DoesNotRouteToADisconnectedWorker()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X86).Fault("crashed");

        factory.Worker(ProcessArchitecture.X64).Report(new ProcessDiscoveredMessage
        {
            ProcessId = 4242,
            Architecture = ProcessArchitecture.X86,
            InjectedLocally = false,
        });

        await WaitForAsync(() => session.Snapshot().Processes.Count > 0);

        factory.Worker(ProcessArchitecture.X86).SentOfType<InjectCommand>().ShouldBeEmpty();
        session.Snapshot().Processes.ShouldHaveSingleItem().Instrumented.ShouldBeFalse();
    }

    [Fact]
    public async Task StoppingAsksWorkersToStopWithoutKillingTheInstaller()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());
        await session.StopAsync();

        foreach (var worker in factory.Created)
        {
            worker.SentOfType<StopCommand>().ShouldHaveSingleItem()
                .TerminateTargets.ShouldBeFalse();
        }

        session.State.ShouldBe(PackagingSessionState.Stopped);
    }

    [Fact]
    public async Task StoppingBeforeTheInstallerExitsIsAllowed()
    {
        // Installers that leave an updater running would otherwise strand the wizard forever.
        var (session, _) = Build();

        await session.StartAsync(Options());

        await Should.NotThrowAsync(() => session.StopAsync());
    }

    [Fact]
    public async Task StoppingTwiceIsHarmless()
    {
        var (session, _) = Build();

        await session.StartAsync(Options());
        await session.StopAsync();

        await Should.NotThrowAsync(() => session.StopAsync());
    }

    [Fact]
    public async Task RefusesToStartTwice()
    {
        var (session, _) = Build();

        await session.StartAsync(Options());

        await Should.ThrowAsync<InvalidOperationException>(() => session.StartAsync(Options()));
    }

    [Fact]
    public async Task FailsWhenNoWorkerCanBeStarted()
    {
        var (session, factory) = Build();

        factory.FailToStart = true;

        await Should.ThrowAsync<InvalidOperationException>(() => session.StartAsync(Options()));

        session.State.ShouldBe(PackagingSessionState.Failed);
    }

    [Fact]
    public async Task ResetClearsEverything()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(Batch(@"C:\Games\Example\game.exe"));

        await session.StopAsync();

        session.Reset();

        var snapshot = session.Snapshot();

        snapshot.Files.ShouldBeEmpty();
        snapshot.Registry.ShouldBeEmpty();
        snapshot.Processes.ShouldBeEmpty();
        snapshot.DroppedEventCount.ShouldBe(0);
        session.State.ShouldBe(PackagingSessionState.Idle);
    }

    [Fact]
    public void ReportsUnsupportedWhenNoWorkersExist()
    {
        var factory = new FakePackagingWorkerFactory { IsSupported = false };
        var session = new PackagingSessionService(
            factory, NullLogger<PackagingSessionService>.Instance);

        session.IsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task ThrowsWhenStartedOnAnUnsupportedHost()
    {
        var factory = new FakePackagingWorkerFactory { IsSupported = false };
        var session = new PackagingSessionService(
            factory, NullLogger<PackagingSessionService>.Instance);

        await Should.ThrowAsync<PlatformNotSupportedException>(() => session.StartAsync(Options()));
    }

    private static (PackagingSessionService Session, FakePackagingWorkerFactory Factory) Build()
    {
        var factory = new FakePackagingWorkerFactory(
            ProcessArchitecture.X64, ProcessArchitecture.X86);

        return (new PackagingSessionService(factory, NullLogger<PackagingSessionService>.Instance), factory);
    }

    private static PackagingSessionOptions Options() => new() { InstallerPath = InstallerPath };

    private static ChangeBatchMessage Batch(params string[] paths) => new()
    {
        Files = [.. paths.Select(p => new FileChange { Verb = "FILE WRITE", Path = p })],
    };

    private static RegistryChange Registry(ProcessArchitecture architecture) => new()
    {
        Verb = "REG WRITE",
        KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Example",
        ValueName = "InstallPath",
        SourceArchitecture = architecture,
    };

    /// <summary>
    /// Some handling is dispatched asynchronously off the message event, so assertions have to
    /// wait for it rather than assume it already ran.
    /// </summary>
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
