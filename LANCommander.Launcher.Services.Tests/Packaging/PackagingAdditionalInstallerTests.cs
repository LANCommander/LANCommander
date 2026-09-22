using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.Changes;
using LANCommander.Packaging.IPC;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// Covers running a second installer through a session that has already captured one: the patch
/// and mod case, where everything has to merge into a single change set without the second run
/// inheriting the first one's process ledger.
/// </summary>
public class PackagingAdditionalInstallerTests
{
    private const string InstallerPath = @"C:\Installers\setup.exe";
    private const string PatchPath = @"C:\Installers\nocd-patch.exe";

    [Fact]
    public async Task KeepsEverythingCapturedByTheFirstInstaller()
    {
        // The whole point of reusing the session: a patch installer's output has to land in the
        // same package as the base install's, not replace it.
        var (session, factory) = Build();

        await session.StartAsync(Options(InstallerPath));

        factory.Created[0].Report(Batch(@"C:\Games\Example\game.exe"));

        await WaitForAsync(() => session.Snapshot().Files.Count == 1);
        await session.StopAsync();

        await session.StartAsync(Options(PatchPath));

        factory.Created[2].Report(Batch(@"C:\Games\Example\ddraw.dll"));

        await WaitForAsync(() => session.Snapshot().Files.Count == 2);

        session.Snapshot().Files.Select(f => f.Path).ShouldBe(
            [@"C:\Games\Example\game.exe", @"C:\Games\Example\ddraw.dll"], ignoreOrder: true);
    }

    [Fact]
    public async Task AdvancesTheRunIdForEachInstaller()
    {
        var (session, _) = Build();

        await session.StartAsync(Options(InstallerPath));

        var first = session.CurrentRunId;

        await session.StopAsync();
        await session.StartAsync(Options(PatchPath));

        session.CurrentRunId.ShouldBeGreaterThan(first);
    }

    [Fact]
    public async Task TagsProcessesWithTheRunTheyBelongedTo()
    {
        // What lets a step tell "the patch installer is still running" from "the base
        // installer's process never reported its exit because its worker was torn down first".
        var (session, factory) = Build();

        await session.StartAsync(Options(InstallerPath));

        var firstRun = session.CurrentRunId;

        factory.Created[0].Report(Discovered(100));

        await WaitForAsync(() => session.Snapshot().Processes.Count == 1);
        await session.StopAsync();

        await session.StartAsync(Options(PatchPath));

        factory.Created[2].Report(Discovered(200));

        await WaitForAsync(() => session.Snapshot().Processes.Count == 2);

        var processes = session.Snapshot().Processes.ToDictionary(p => p.ProcessId);

        processes[100].RunId.ShouldBe(firstRun);
        processes[200].RunId.ShouldBe(session.CurrentRunId);
    }

    [Fact]
    public async Task CountsOnlyTheCurrentRunsProcesses()
    {
        // A step showing "2 processes" for a patch installer that spawned one would read as a
        // capture problem. File counts stay cumulative because the package is cumulative.
        var (session, factory) = Build();

        await session.StartAsync(Options(InstallerPath));

        factory.Created[0].Report(Discovered(100));
        factory.Created[0].Report(Batch(@"C:\Games\Example\game.exe"));

        await WaitForAsync(() => session.Snapshot().Processes.Count == 1);
        await session.StopAsync();

        await session.StartAsync(Options(PatchPath));

        factory.Created[2].Report(Discovered(200));
        factory.Created[2].Report(Batch(@"C:\Games\Example\ddraw.dll"));

        PackagingCounters? latest = null;

        session.CountersChanged += (_, counters) => latest = counters;

        await WaitForAsync(() => latest is { ProcessCount: 1, FileCount: 2 });

        latest!.ProcessCount.ShouldBe(1);
        latest.FileCount.ShouldBe(2);
    }

    [Fact]
    public async Task ResetClearsCapturedChangesButNotTheRunCounter()
    {
        // Run ids never rewind. The session is a singleton and steps remember the run they own,
        // so restarting the counter would let a step from a discarded package match a run in
        // the next one and start swallowing its events.
        var (session, factory) = Build();

        await session.StartAsync(Options(InstallerPath));

        factory.Created[0].Report(Batch(@"C:\Games\Example\game.exe"));

        await WaitForAsync(() => session.Snapshot().Files.Count == 1);
        await session.StopAsync();

        var before = session.CurrentRunId;

        session.Reset();

        session.Snapshot().Files.ShouldBeEmpty();

        await session.StartAsync(Options(PatchPath));

        session.CurrentRunId.ShouldBeGreaterThan(before);
    }

    private static (PackagingSessionService Session, FakePackagingWorkerFactory Factory) Build()
    {
        var factory = new FakePackagingWorkerFactory(
            ProcessArchitecture.X64, ProcessArchitecture.X86);

        return (new PackagingSessionService(factory, NullLogger<PackagingSessionService>.Instance), factory);
    }

    private static PackagingSessionOptions Options(string installerPath) =>
        new() { InstallerPath = installerPath };

    private static ChangeBatchMessage Batch(params string[] paths) => new()
    {
        Files = [.. paths.Select(p => new FileChange { Verb = "FILE WRITE", Path = p })],
    };

    private static ProcessDiscoveredMessage Discovered(int processId) => new()
    {
        ProcessId = processId,
        ImagePath = @"C:\Installers\setup.exe",
        Architecture = ProcessArchitecture.X64,
        InjectedLocally = true,
    };

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
