using LANCommander.Packaging.Changes;
using LANCommander.Packaging.IPC;
using Shouldly;

namespace LANCommander.Packaging.Tests;

/// <summary>
/// Framing tests run over a plain stream — no pipes, no processes, no injection.
/// </summary>
public class PackagingMessageChannelTests
{
    [Fact]
    public async Task RoundTripsACommand()
    {
        var sent = new LaunchInstallerCommand
        {
            CorrelationId = Guid.NewGuid(),
            ExecutablePath = @"C:\Installers\setup.exe",
            Arguments = "/S",
            WorkingDirectory = @"C:\Installers",
        };

        var received = await RoundTripAsync(sent);

        var command = received.ShouldBeOfType<LaunchInstallerCommand>();

        command.CorrelationId.ShouldBe(sent.CorrelationId);
        command.ExecutablePath.ShouldBe(sent.ExecutablePath);
        command.Arguments.ShouldBe(sent.Arguments);
        command.WorkingDirectory.ShouldBe(sent.WorkingDirectory);
    }

    [Fact]
    public async Task RoundTripsAChangeBatch()
    {
        var sent = new ChangeBatchMessage
        {
            Files =
            [
                new FileChange { Verb = "FILE WRITE", Path = @"C:\Games\Example\game.exe", ProcessId = 42 },
            ],
            Registry =
            [
                new RegistryChange
                {
                    Verb = "REG WRITE",
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Example",
                    ValueName = "InstallPath",
                    SourceArchitecture = ProcessArchitecture.X86,
                    ProcessId = 42,
                },
            ],
            DroppedCount = 7,
        };

        var batch = (await RoundTripAsync(sent)).ShouldBeOfType<ChangeBatchMessage>();

        batch.Files.ShouldHaveSingleItem().Path.ShouldBe(@"C:\Games\Example\game.exe");
        batch.Registry.ShouldHaveSingleItem().SourceArchitecture.ShouldBe(ProcessArchitecture.X86);
        batch.DroppedCount.ShouldBe(7);
    }

    [Fact]
    public async Task PreservesDerivedTypeAcrossManyMessages()
    {
        var stream = new MemoryStream();

        using (var writer = new PackagingMessageChannel(stream, leaveOpen: true))
        {
            await writer.WriteAsync(new PingCommand { Sequence = 1 });
            await writer.WriteAsync(new PongMessage { Sequence = 1 });
            await writer.WriteAsync(new WorkerStoppedMessage { Reason = "done" });
        }

        stream.Position = 0;

        using var reader = new PackagingMessageChannel(stream, leaveOpen: true);

        (await reader.ReadAsync()).ShouldBeOfType<PingCommand>().Sequence.ShouldBe(1);
        (await reader.ReadAsync()).ShouldBeOfType<PongMessage>().Sequence.ShouldBe(1);
        (await reader.ReadAsync()).ShouldBeOfType<WorkerStoppedMessage>().Reason.ShouldBe("done");
    }

    [Fact]
    public async Task ReturnsNullAtEndOfStream()
    {
        using var reader = new PackagingMessageChannel(new MemoryStream(), leaveOpen: true);

        (await reader.ReadAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNullOnAFrameTruncatedMidPayload()
    {
        // A worker killed mid-write is expected, not exceptional.
        var stream = new MemoryStream();

        using (var writer = new PackagingMessageChannel(stream, leaveOpen: true))
            await writer.WriteAsync(new PingCommand { Sequence = 1 });

        var truncated = new MemoryStream(stream.ToArray()[..^4]);

        using var reader = new PackagingMessageChannel(truncated, leaveOpen: true);

        (await reader.ReadAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task RejectsAnImplausibleFrameLength()
    {
        // Garbage on the wire means the channel is out of sync and cannot be resynchronized,
        // so it must fail loudly rather than try to allocate whatever it was told to.
        var stream = new MemoryStream(BitConverter.GetBytes(int.MaxValue));

        using var reader = new PackagingMessageChannel(stream, leaveOpen: true);

        await Should.ThrowAsync<InvalidDataException>(() => reader.ReadAsync());
    }

    [Fact]
    public async Task RejectsANegativeFrameLength()
    {
        var stream = new MemoryStream(BitConverter.GetBytes(-1));

        using var reader = new PackagingMessageChannel(stream, leaveOpen: true);

        await Should.ThrowAsync<InvalidDataException>(() => reader.ReadAsync());
    }

    [Fact]
    public async Task ConcurrentWritesDoNotInterleaveFrames()
    {
        var stream = new MemoryStream();

        using (var writer = new PackagingMessageChannel(stream, leaveOpen: true))
        {
            await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
                writer.WriteAsync(new PingCommand { Sequence = i })));
        }

        stream.Position = 0;

        using var reader = new PackagingMessageChannel(stream, leaveOpen: true);

        var sequences = new List<long>();

        while (await reader.ReadAsync() is PingCommand ping)
            sequences.Add(ping.Sequence);

        sequences.Count.ShouldBe(50);
        sequences.Order().ShouldBe(Enumerable.Range(0, 50).Select(i => (long)i));
    }

    private static async Task<PackagingMessage> RoundTripAsync(PackagingMessage message)
    {
        var stream = new MemoryStream();

        using (var writer = new PackagingMessageChannel(stream, leaveOpen: true))
            await writer.WriteAsync(message);

        stream.Position = 0;

        using var reader = new PackagingMessageChannel(stream, leaveOpen: true);

        return (await reader.ReadAsync()).ShouldNotBeNull();
    }
}
