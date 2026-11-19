using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests;

/// <summary>
/// A game runs until the last process it started exits, the way a launcher such as Goldberg's
/// ColdClientLoader starts the real game and leaves. Windows only: the trees are built from cmd.exe.
/// </summary>
public class ProcessTreeTests
{
    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    /// <summary>
    /// cmd.exe that starts a middle cmd.exe, which starts ping in the background and exits at once, as
    /// does the first: ping is left an orphan of an orphan, like a game whose loader has gone.
    /// </summary>
    private static Process StartOrphaningTree(int pingSeconds)
    {
        var startInfo = new ProcessStartInfo(Cmd, $"/c {Cmd} /c start \"\" /b ping -n {pingSeconds + 1} 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };

        return Process.Start(startInfo)!;
    }

    [Fact]
    public async Task TheGameRunsUntilAProcessItsLauncherLeftBehindExits()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var launchedAt = DateTime.UtcNow;
        using var root = StartOrphaningTree(pingSeconds: 3);
        using var tree = new ProcessTree(root, launchedAt);

        var stopwatch = Stopwatch.StartNew();

        await tree.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(root.HasExited);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(2), $"Returned after {stopwatch.Elapsed}, before ping exited");
        Assert.Contains(tree.ProcessIds, id => id != root.Id);
    }

    [Fact]
    public async Task StoppingTheGameKillsTheProcessesItsLauncherLeftBehind()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var launchedAt = DateTime.UtcNow;
        using var root = StartOrphaningTree(pingSeconds: 60);
        using var tree = new ProcessTree(root, launchedAt);
        using var cancellation = new CancellationTokenSource();

        var waiting = tree.WaitForExitAsync(cancellation.Token);

        // Until the tree has found ping
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!root.HasExited || tree.ProcessIds.Count < 2)
        {
            Assert.True(DateTime.UtcNow < deadline, "ping was never found in the tree");
            await Task.Delay(100);
        }

        var tracked = tree.ProcessIds.ToArray();

        await cancellation.CancelAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(tree.IsRunning);

        foreach (var id in tracked.Where(id => id != root.Id))
        {
            // Gone, or a different process has since taken the ID
            try
            {
                using var process = Process.GetProcessById(id);
                Assert.True(process.HasExited || process.StartTime.ToUniversalTime() > DateTime.UtcNow.AddSeconds(-5));
            }
            catch (ArgumentException)
            {
            }
        }
    }

    [Fact]
    public async Task StoppingTheGameWhileItsLauncherWaitsOnItKillsBoth()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // The first cmd.exe waits on the second, which waits on ping, as a loader waits on its game
        var launchedAt = DateTime.UtcNow;
        using var root = Process.Start(new ProcessStartInfo(Cmd, $"/c {Cmd} /c ping -n 61 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;

        using var tree = new ProcessTree(root, launchedAt);
        using var cancellation = new CancellationTokenSource();

        var waiting = tree.WaitForExitAsync(cancellation.Token);

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (tree.ProcessIds.Count < 3)
        {
            Assert.True(DateTime.UtcNow < deadline, "The tree never found the loader's children");
            await Task.Delay(100);
        }

        await cancellation.CancelAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(root.HasExited);
        Assert.False(tree.IsRunning);
    }
}
