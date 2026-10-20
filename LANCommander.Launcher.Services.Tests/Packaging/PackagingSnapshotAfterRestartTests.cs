using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.Changes;
using LANCommander.Packaging.IPC;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// The counters and the snapshot must never disagree.
/// </summary>
/// <remarks>
/// Reported symptom: monitoring showed 7 registry entries captured, but the registry step was
/// skipped as though none had been. The session keeps everything it captured across an elevated
/// restart, so a snapshot taken afterwards has to include changes recorded before it.
/// </remarks>
public class PackagingSnapshotAfterRestartTests
{
    [Fact]
    public async Task SnapshotMatchesTheCountersAfterAnElevatedRestart()
    {
        var (session, factory) = Build();

        var counters = 0;

        session.CountersChanged += (_, c) => Volatile.Write(ref counters, c.RegistryCount);

        await session.StartAsync(Options());

        // Three captured before the installer escalated.
        factory.Worker(ProcessArchitecture.X64).Report(RegistryBatch(1, 2, 3));

        await session.RestartElevatedAsync();

        // Four more captured by the elevated run.
        factory.Created.Last(w => w.Architecture == ProcessArchitecture.X64 && w.IsElevated)
            .Report(RegistryBatch(4, 5, 6, 7));

        var snapshot = session.Snapshot();

        snapshot.Registry.Count.ShouldBe(7);

        await WaitForAsync(() => Volatile.Read(ref counters) == 7);

        Volatile.Read(ref counters).ShouldBe(snapshot.Registry.Count);
    }

    [Fact]
    public async Task ChangesCapturedBeforeElevationSurviveTheRestart()
    {
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(RegistryBatch(1, 2, 3));

        await session.RestartElevatedAsync();

        session.Snapshot().Registry.Count.ShouldBe(3);
    }

    [Fact]
    public async Task StoppingTwiceKeepsWhatWasCaptured()
    {
        // A self-elevating installer's stub exits, then the elevated run exits later, so the
        // session can legitimately be stopped more than once.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        factory.Worker(ProcessArchitecture.X64).Report(RegistryBatch(1, 2, 3));

        await session.StopAsync();
        await session.StopAsync();

        session.Snapshot().Registry.Count.ShouldBe(3);
    }

    private static ChangeBatchMessage RegistryBatch(params int[] ids) => new()
    {
        Registry =
        [
            .. ids.Select(i => new RegistryChange
            {
                Verb = "REG WRITE",
                KeyPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Example\Key{i}",
                ValueName = $"Value{i}",
                SourceArchitecture = ProcessArchitecture.X64,
            }),
        ],
    };

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

        condition().ShouldBeTrue("Timed out waiting for counters to settle.");
    }
}
