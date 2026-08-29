using System.Collections.Concurrent;
using LANCommander.Packaging.Changes;
using LANCommander.Packaging.Ipc;
using LANCommander.Packaging.Worker;
using Shouldly;

namespace LANCommander.Packaging.Worker.Tests;

public class ChangeCollectorTests
{
    [Fact]
    public async Task ForwardsWriteEvents()
    {
        await using var harness = new Harness();

        harness.Collector.AddFile("FILE WRITE", @"C:\Games\Example\game.exe", 42);

        var batch = await harness.WaitForBatchAsync();

        batch.Files.ShouldHaveSingleItem().Path.ShouldBe(@"C:\Games\Example\game.exe");
    }

    [Fact]
    public async Task DiscardsReadEvents()
    {
        // Reads are the overwhelming majority of what an installer generates. Dropping them at
        // the source is what keeps the channel from being the bottleneck.
        await using var harness = new Harness();

        harness.Collector.AddFile("FILE READ", @"C:\Windows\System32\kernel32.dll", 42);
        harness.Collector.AddFile("FILE WRITE", @"C:\Games\Example\game.exe", 42);

        var batch = await harness.WaitForBatchAsync();

        batch.Files.ShouldHaveSingleItem().Verb.ShouldBe("FILE WRITE");
    }

    [Fact]
    public async Task DiscardsIgnoredPaths()
    {
        await using var harness = new Harness();

        harness.Collector.Filter = new ChangeFilter { IgnoredPathPrefixes = [@"C:\Temp"] };

        harness.Collector.AddFile("FILE WRITE", @"C:\Temp\extracted.tmp", 42);
        harness.Collector.AddFile("FILE WRITE", @"C:\Games\Example\game.exe", 42);

        var batch = await harness.WaitForBatchAsync();

        batch.Files.ShouldHaveSingleItem().Path.ShouldBe(@"C:\Games\Example\game.exe");
    }

    [Fact]
    public async Task CollapsesRepeatsOfTheSamePathAndVerb()
    {
        await using var harness = new Harness();

        for (var i = 0; i < 100; i++)
            harness.Collector.AddFile("FILE WRITE", @"C:\Games\Example\game.exe", 42);

        var batch = await harness.WaitForBatchAsync();

        batch.Files.Length.ShouldBe(1);
    }

    [Fact]
    public async Task CollapsesPathsThatDifferOnlyInForm()
    {
        await using var harness = new Harness();

        harness.Collector.AddFile("FILE WRITE", @"C:\Games\Example\game.exe", 42);
        harness.Collector.AddFile("FILE WRITE", @"\\?\C:\Games\Example\game.exe", 42);
        harness.Collector.AddFile("FILE WRITE", @"C:\Games\Example\..\Example\game.exe", 42);

        var batch = await harness.WaitForBatchAsync();

        batch.Files.Length.ShouldBe(1);
    }

    [Fact]
    public async Task ReportsAPathAgainWhenItsVerbEscalates()
    {
        await using var harness = new Harness();

        harness.Collector.AddFile("FILE COPY", @"C:\Games\Example\game.exe", 42);
        harness.Collector.AddFile("FILE WRITE", @"C:\Games\Example\game.exe", 42);

        var batch = await harness.WaitForBatchAsync();

        batch.Files.Length.ShouldBe(2);
    }

    [Fact]
    public async Task CollapsesRepeatsOfTheSameRegistryValue()
    {
        // The old collector used a bag with no deduplication, so an installer writing the same
        // value repeatedly produced one tree row per write.
        await using var harness = new Harness();

        for (var i = 0; i < 100; i++)
        {
            harness.Collector.AddRegistry(
                "REG WRITE", @"HKEY_LOCAL_MACHINE\SOFTWARE\Example", "InstallPath",
                42, ProcessArchitecture.X86);
        }

        var batch = await harness.WaitForBatchAsync();

        batch.Registry.Length.ShouldBe(1);
    }

    [Fact]
    public async Task KeepsDistinctValuesUnderOneKey()
    {
        await using var harness = new Harness();

        harness.Collector.AddRegistry(
            "REG WRITE", @"HKEY_LOCAL_MACHINE\SOFTWARE\Example", "A", 42, ProcessArchitecture.X86);
        harness.Collector.AddRegistry(
            "REG WRITE", @"HKEY_LOCAL_MACHINE\SOFTWARE\Example", "B", 42, ProcessArchitecture.X86);

        var batch = await harness.WaitForBatchAsync();

        batch.Registry.Length.ShouldBe(2);
    }

    [Fact]
    public async Task TagsRegistryChangesWithTheCapturingArchitecture()
    {
        // Script generation needs this to decide whether to apply WOW64 redirection.
        await using var harness = new Harness();

        harness.Collector.AddRegistry(
            "REG CREATE", @"HKEY_LOCAL_MACHINE\SOFTWARE\Example", "", 42, ProcessArchitecture.X86);

        var batch = await harness.WaitForBatchAsync();

        batch.Registry.ShouldHaveSingleItem().SourceArchitecture.ShouldBe(ProcessArchitecture.X86);
    }

    [Fact]
    public async Task FlushDeliversWhatIsStillBuffered()
    {
        // The tail of a capture would otherwise be lost when the installer exits.
        await using var harness = new Harness();

        harness.Collector.AddFile("FILE WRITE", @"C:\Games\Example\game.exe", 42);

        await harness.Collector.FlushAsync();

        harness.Batches.SelectMany(b => b.Files).ShouldContain(f => f.Path.EndsWith("game.exe"));
    }

    [Fact]
    public async Task IngestNeverBlocksOnASlowConsumer()
    {
        // Ingest runs on the Interposer's pipe-reader task; blocking it stalls the hooked
        // process inside WriteFile. Overflow must drop, not wait.
        var gate = new TaskCompletionSource();

        await using var harness = new Harness(async (_, _) => await gate.Task);

        var start = Environment.TickCount64;

        for (var i = 0; i < PackagingProtocol.ChangeQueueCapacity + 5_000; i++)
            harness.Collector.AddFile("FILE WRITE", $@"C:\Games\Example\file{i}.dat", 42);

        var elapsed = Environment.TickCount64 - start;

        gate.SetResult();

        elapsed.ShouldBeLessThan(20_000);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Harness(Func<ChangeBatchMessage, CancellationToken, Task>? flush = null)
        {
            Collector = new ChangeCollector(async (batch, ct) =>
            {
                Batches.Add(batch);

                _received.TrySetResult(batch);

                if (flush != null)
                    await flush(batch, ct);
            })
            {
                Filter = new ChangeFilter(),
            };
        }

        private readonly TaskCompletionSource<ChangeBatchMessage> _received = new();

        public ChangeCollector Collector { get; }

        public ConcurrentBag<ChangeBatchMessage> Batches { get; } = [];

        public async Task<ChangeBatchMessage> WaitForBatchAsync()
        {
            var completed = await Task.WhenAny(_received.Task, Task.Delay(TimeSpan.FromSeconds(10)));

            completed.ShouldBe(_received.Task, "No change batch was flushed within the timeout.");

            return await _received.Task;
        }

        public ValueTask DisposeAsync() => Collector.DisposeAsync();
    }
}
