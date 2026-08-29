using LANCommander.Packaging.Worker;
using Shouldly;

namespace LANCommander.Packaging.Worker.Tests;

/// <summary>
/// Subtree walking is a pure function over a process snapshot, so it can be tested without
/// starting anything.
/// </summary>
public class ProcessInspectorTests
{
    [Fact]
    public void FindsChildrenAndGrandchildren()
    {
        // An installer that extracts to temp and runs a second stage, which in turn runs a
        // redistributable, is the normal case — not an edge case.
        var snapshot = Snapshot(
            (100, 1, "setup.exe"),
            (200, 100, "setup_stage2.tmp"),
            (300, 200, "vcredist.exe"),
            (999, 1, "unrelated.exe"));

        var descendants = ProcessInspector.GetDescendants(snapshot, 100);

        descendants.Select(d => d.ProcessId).Order().ShouldBe([200, 300]);
    }

    [Fact]
    public void ExcludesTheRootItself()
    {
        var snapshot = Snapshot((100, 1, "setup.exe"), (200, 100, "child.exe"));

        ProcessInspector.GetDescendants(snapshot, 100)
            .ShouldNotContain(d => d.ProcessId == 100);
    }

    [Fact]
    public void ReturnsNothingForALeaf()
    {
        var snapshot = Snapshot((100, 1, "setup.exe"));

        ProcessInspector.GetDescendants(snapshot, 100).ShouldBeEmpty();
    }

    [Fact]
    public void ReturnsNothingForAnUnknownRoot()
    {
        var snapshot = Snapshot((100, 1, "setup.exe"));

        ProcessInspector.GetDescendants(snapshot, 12345).ShouldBeEmpty();
    }

    [Fact]
    public void TerminatesOnACyclicTree()
    {
        // Process ids get recycled, so a snapshot can contain a parent chain that loops. The
        // walk has to terminate rather than hang the poll loop.
        var snapshot = Snapshot(
            (100, 200, "a.exe"),
            (200, 100, "b.exe"));

        var descendants = ProcessInspector.GetDescendants(snapshot, 100);

        descendants.Select(d => d.ProcessId).ShouldBe([200]);
    }

    [Fact]
    public void HandlesWideTrees()
    {
        var entries = new List<(int, int, string)> { (100, 1, "setup.exe") };

        for (var i = 0; i < 50; i++)
            entries.Add((200 + i, 100, $"child{i}.exe"));

        ProcessInspector.GetDescendants(Snapshot([.. entries]), 100).Count.ShouldBe(50);
    }

    private static List<ProcessInspector.ProcessEntry> Snapshot(
        params (int ProcessId, int ParentProcessId, string Name)[] entries) =>
        [.. entries.Select(e => new ProcessInspector.ProcessEntry(e.ProcessId, e.ParentProcessId, e.Name))];
}
