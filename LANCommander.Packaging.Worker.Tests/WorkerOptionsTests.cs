using LANCommander.Packaging.Worker;
using Shouldly;

namespace LANCommander.Packaging.Worker.Tests;

public class WorkerOptionsTests
{
    [Fact]
    public void ParsesTheRequiredArguments()
    {
        var options = WorkerOptions.Parse(["--pipe", "test-pipe", "--token", "abc", "--host-pid", "1234"]);

        options.ShouldNotBeNull();
        options.PipeName.ShouldBe("test-pipe");
        options.Token.ShouldBe("abc");
        options.HostProcessId.ShouldBe(1234);
    }

    public static TheoryData<string[]> IncompleteArguments
    {
        get
        {
            var data = new TheoryData<string[]>();

            data.Add([]);
            data.Add(["--pipe", "test-pipe"]);
            data.Add(["--token", "abc", "--host-pid", "1234"]);
            data.Add(["--pipe", "test-pipe", "--token", "abc"]);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(IncompleteArguments))]
    public void RejectsIncompleteArguments(string[] args)
    {
        // A worker with no host pid could outlive the launcher, and one with no token cannot
        // prove who it is. Neither should start at all.
        WorkerOptions.Parse(args).ShouldBeNull();
    }

    [Fact]
    public void RejectsANonNumericHostProcessId()
    {
        WorkerOptions.Parse(["--pipe", "p", "--token", "t", "--host-pid", "not-a-number"]).ShouldBeNull();
    }

    [Fact]
    public void IgnoresAFlagMissingItsValue()
    {
        var options = WorkerOptions.Parse(
            ["--pipe", "p", "--token", "t", "--host-pid", "1", "--interposer-dll"]);

        options.ShouldNotBeNull();
        options.InterposerDllPath.ShouldBeNull();
    }

    [Fact]
    public void RecognizesACompleteSpawnRequest()
    {
        var options = WorkerOptions.Parse(
        [
            "--pipe", "p", "--token", "t", "--host-pid", "1",
            "--spawn-worker", @"C:\Workers\win-x86\worker.exe",
            "--spawn-worker-pipe", "sibling-pipe",
            "--spawn-worker-token", "sibling-token",
        ]);

        options.ShouldNotBeNull();
        options.HasSpawnRequest.ShouldBeTrue();
        options.SpawnWorkerPipeName.ShouldBe("sibling-pipe");
    }

    [Fact]
    public void IgnoresAPartialSpawnRequest()
    {
        // Spawning with a pipe name but no token would produce a worker that can never complete
        // its handshake.
        var options = WorkerOptions.Parse(
        [
            "--pipe", "p", "--token", "t", "--host-pid", "1",
            "--spawn-worker", @"C:\Workers\win-x86\worker.exe",
        ]);

        options.ShouldNotBeNull();
        options.HasSpawnRequest.ShouldBeFalse();
    }
}
