using System.Diagnostics;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Hosting;

namespace LANCommander.SDK.Tests.PowerShell.Debugging;

[CollectionDefinition("PowerShellDebugger", DisableParallelization = true)]
public sealed class PowerShellDebuggerCollection;

/// <summary>
/// Headless end-to-end checks for the debug engine: breakpoint binding, the DebuggerStop pump, stepping,
/// watch evaluation, per-run runspace freshness, cancellation and detaching.
/// </summary>
[Collection("PowerShellDebugger")]
public sealed class DebugSessionScenarioTests : IDisposable
{
    private readonly string _workspace;

    public DebugSessionScenarioTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"lc-psdbg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspace);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workspace, recursive: true); }
        catch (Exception) { }
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_workspace, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void NoBreakpoints_RoutesEveryStreamToTheSink()
    {
        var path = Write("plain.ps1", """
            Write-Host 'from write-host'
            Write-Output 'from write-output'
            Write-Warning 'from write-warning'
            Write-Verbose 'from write-verbose' -Verbose
            'bare expression'
            """);

        using var harness = new DebugSessionHarness(path);
        harness.Start([]);

        Assert.True(harness.WaitForCompletion());

        // Write-Host reaches the host UI and is also republished on the Information stream tagged
        // PSHOST. Without the filter every line would appear twice.
        Assert.Single(harness.Sink.TextOf(ConsoleOutputKind.Host), t => t.Contains("from write-host"));
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("from write-output"));
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("bare expression"));
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Warning), t => t.Contains("from write-warning"));
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Verbose), t => t.Contains("from write-verbose"));
    }

    [Fact]
    public void Breakpoint_BindsToCompiledContents_AndSupportsInspection()
    {
        var path = Write("loop.ps1", """
            $total = 0
            $items = @()
            foreach ($i in 1..3) {
                $total += $i
                $items += [pscustomobject]@{ Index = $i; Running = $total }
            }
            Write-Output "done $total"
            """);

        using var harness = new DebugSessionHarness(path);

        // Line 4 is "$total += $i", inside the loop.
        harness.Start([new BreakpointRequest(path, 4, true)]);

        var stop = harness.WaitForStop();
        Assert.NotNull(stop);

        Assert.Equal(4, stop.LineNumber);
        Assert.True(string.Equals(Path.GetFullPath(stop.ScriptName!), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase),
            $"stopped in {stop.ScriptName}");
        Assert.Single(stop.HitBreakpointIds);
        Assert.NotEmpty(stop.Frames);

        var locals = harness.FrameVariables(0);
        var total = locals.FirstOrDefault(v => v.Name == "total");
        Assert.NotNull(total);
        Assert.Equal("0", total.Value);
        Assert.DoesNotContain(locals, v => v.Name == "PSVersionTable");
        Assert.DoesNotContain(locals, v => v.Name == DebugSession.CompiledScriptVariable);

        var watch = harness.Evaluate("$total + 100");
        Assert.False(watch.IsError);
        Assert.Equal("100", watch.Output.Trim());

        var count = harness.Evaluate("$items.Count");
        Assert.False(count.IsError);
        Assert.Equal("0", count.Output.Trim());

        harness.Session.Resume(DebugResumeKind.Continue);
        Assert.NotNull(harness.WaitForStop());

        var items = harness.FrameVariables(0).FirstOrDefault(v => v.Name == "items");
        Assert.True(items?.HasChildren);
        Assert.NotEmpty(harness.Expand(items!.Handle!.Value));

        harness.Session.Resume(DebugResumeKind.Continue);
        harness.DrainStops();

        Assert.True(harness.WaitForCompletion());
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("done 6"));
    }

    [Fact]
    public void Breakpoint_BindsWhenTheFileOnDiskDiffersFromWhatRuns()
    {
        // The debugger compiles the text it is handed against the path; the file on disk is never read.
        // This is what lets a script from the server, or an unsaved buffer, be debugged under its real path.
        var path = Write("stale.ps1", "Write-Output 'stale copy on disk'");

        using var harness = new DebugSessionHarness(path);

        harness.Start([new BreakpointRequest(path, 2, true)], contents: "$first = 1\n$second = 2\nWrite-Output 'fresh'");

        var stop = harness.WaitForStop();
        Assert.NotNull(stop);
        Assert.Equal(2, stop.LineNumber);

        harness.Session.Resume(DebugResumeKind.Continue);
        harness.DrainStops();

        Assert.True(harness.WaitForCompletion());
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("fresh"));
        Assert.DoesNotContain(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("stale"));
    }

    [Fact]
    public void Stepping_IntoOverAndOutOfAFunction()
    {
        var path = Write("steps.ps1", """
            function Get-Square {
                param([int]$Value)
                $squared = $Value * $Value
                return $squared
            }

            $a = 2
            $b = Get-Square -Value $a
            Write-Output "b=$b"
            """);

        using var harness = new DebugSessionHarness(path);

        // Line 8 is "$b = Get-Square -Value $a".
        harness.Start([new BreakpointRequest(path, 8, true)]);

        var stop = harness.WaitForStop();
        Assert.NotNull(stop);

        var depthBefore = stop.Frames.Count;

        harness.Session.Resume(DebugResumeKind.StepInto);
        var inside = harness.WaitForStop();
        Assert.NotNull(inside);

        Assert.True(inside.Frames.Count > depthBefore, $"{depthBefore} -> {inside.Frames.Count}");
        Assert.Contains("Get-Square", inside.Frames[0].FunctionName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("2", harness.FrameVariables(0).FirstOrDefault(v => v.Name == "Value")?.Value);

        // Exactly where step-into lands is engine-defined, so step until the assignment has run.
        var squaredValue = string.Empty;

        for (var attempt = 0; attempt < 4; attempt++)
        {
            harness.Session.Resume(DebugResumeKind.StepOver);

            if (harness.WaitForStop() is null)
                break;

            squaredValue = harness.Evaluate("$squared").Output.Trim();

            if (squaredValue == "4")
                break;
        }

        Assert.Equal("4", squaredValue);

        harness.Session.Resume(DebugResumeKind.StepOut);
        var afterOut = harness.WaitForStop();
        Assert.True(afterOut is null || afterOut.Frames.Count <= depthBefore);

        harness.Session.Resume(DebugResumeKind.Continue);
        harness.DrainStops();

        Assert.True(harness.WaitForCompletion());
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("b=4"));
    }

    [Fact]
    public void StepIntoOnStart_StopsOnTheScriptsFirstLine_NotTheWrapper()
    {
        var path = Write("first.ps1", """
            $one = 1
            $two = 2
            """);

        using var harness = new DebugSessionHarness(path);
        harness.Start([], stepIntoOnStart: true);

        var stop = harness.WaitForStop();
        Assert.NotNull(stop);
        Assert.Equal(1, stop.LineNumber);
        Assert.True(string.Equals(stop.ScriptName, path, StringComparison.OrdinalIgnoreCase), $"stopped in {stop.ScriptName}");

        harness.Session.Resume(DebugResumeKind.Continue);
        harness.DrainStops();

        Assert.True(harness.WaitForCompletion());
    }

    [Fact]
    public void EachRun_GetsAFreshRunspace()
    {
        var path = Write("fresh.ps1", """
            $marker = 'first'
            Write-Output "leaked=$($global:Leaked)"
            """);

        using var harness = new DebugSessionHarness(path);
        harness.Start([new BreakpointRequest(path, 2, true)]);

        Assert.NotNull(harness.WaitForStop());

        var planted = harness.Evaluate("$global:Leaked = 'yes'; $global:Leaked");
        Assert.False(planted.IsError);
        Assert.Contains("yes", planted.Output);

        harness.Session.Resume(DebugResumeKind.Continue);
        harness.DrainStops();
        Assert.True(harness.WaitForCompletion());
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("leaked=yes"));

        // Second run on the SAME session object: the runspace must be brand new.
        harness.Sink.Clear();
        harness.Reset();
        harness.Start([new BreakpointRequest(path, 2, true)]);

        Assert.NotNull(harness.WaitForStop());
        Assert.Equal(string.Empty, harness.Evaluate("[string]$global:Leaked").Output.Trim());

        harness.Session.Resume(DebugResumeKind.Continue);
        harness.DrainStops();
        Assert.True(harness.WaitForCompletion());

        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("leaked="));
        Assert.DoesNotContain(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("leaked=yes"));
    }

    [Fact]
    public void CommandCatalog_ListsAndDescribesCommandsWithNoSessionRunning()
    {
        using var catalog = DebugSessionHarness.CreateCatalog();

        var commands = catalog.ListAsync().GetAwaiter().GetResult();
        Assert.NotEmpty(commands);

        var childItem = commands.FirstOrDefault(c => c.Name == "Get-ChildItem");
        Assert.NotNull(childItem);
        Assert.Equal("Cmdlet", childItem.CommandType);
        Assert.Equal("Microsoft.PowerShell.Management", childItem.ModuleName);

        // The catalogue is built the same way script runspaces are, so LANCommander's cmdlets show up.
        Assert.Contains(commands, c => c.Name == "Get-GameManifest");

        Assert.Contains(commands, c => c.CommandType == "Function");
        Assert.Contains(commands, c => c.CommandType == "Alias");
        Assert.DoesNotContain(commands, c => c.CommandType == "Application");

        var detail = catalog.DescribeAsync("Get-ChildItem").GetAwaiter().GetResult();
        Assert.NotNull(detail);
        Assert.Contains("Get-ChildItem", detail.Syntax);
        Assert.Contains(detail.Parameters, p => p.Name == "Path");
        Assert.DoesNotContain(detail.Parameters, p => p.Name is "ErrorAction" or "Verbose");

        Assert.Null(catalog.DescribeAsync("Get-NoSuchCommandAnywhere").GetAwaiter().GetResult());
        Assert.Equal(commands.Count, catalog.ListAsync().GetAwaiter().GetResult().Count);
    }

    [Fact]
    public void StoppedSession_SeesModulesTheScriptImported()
    {
        var modulePath = Write("Probe.psm1", """
            function Get-ImportedProbe { 'probe' }
            Export-ModuleMember -Function Get-ImportedProbe
            """);

        var path = Write("importer.ps1", $"""
            Import-Module '{modulePath}'
            $ready = $true
            Get-ImportedProbe
            """);

        using (var catalog = DebugSessionHarness.CreateCatalog())
            Assert.DoesNotContain(catalog.ListAsync().GetAwaiter().GetResult(), c => c.Name == "Get-ImportedProbe");

        using var harness = new DebugSessionHarness(path);

        // Line 2 runs after the import.
        harness.Start([new BreakpointRequest(path, 2, true)]);
        Assert.NotNull(harness.WaitForStop());

        var commands = harness.Commands();
        Assert.Contains(commands, c => c.Name == "Get-ImportedProbe");
        Assert.Contains(commands, c => c.Name == "Get-ChildItem");
        Assert.NotNull(harness.CommandDetail("Get-ImportedProbe"));

        harness.Session.Resume(DebugResumeKind.Continue);
        harness.DrainStops();

        Assert.True(harness.WaitForCompletion());
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("probe"));
    }

    [Fact]
    public void Stop_TerminatesASleepingScript()
    {
        var path = Write("sleep.ps1", """
            Write-Output 'before'
            Start-Sleep -Seconds 30
            Write-Output 'after'
            """);

        using var harness = new DebugSessionHarness(path);
        harness.Start([]);

        Assert.True(harness.WaitForState(DebugSessionState.Running, TimeSpan.FromSeconds(20)));

        var stopwatch = Stopwatch.StartNew();
        harness.Session.RequestStop();
        var completed = harness.WaitForCompletion(TimeSpan.FromSeconds(15));
        stopwatch.Stop();

        Assert.True(completed);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(12), $"took {stopwatch.Elapsed.TotalSeconds:0.0}s");
        Assert.DoesNotContain(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("after"));
        Assert.Equal(DebugSessionState.Idle, harness.Session.State);
    }

    [Fact]
    public void Stop_ReleasesAScriptBlockedOnReadHost()
    {
        var path = Write("readhost.ps1", """
            $answer = Read-Host 'Say something'
            Write-Output "got $answer"
            """);

        using var harness = new DebugSessionHarness(path);

        // No queued answer, so the pipeline blocks in ReadLine. Cancelling must release it.
        harness.Start([]);
        Assert.True(harness.WaitForState(DebugSessionState.Running, TimeSpan.FromSeconds(20)));

        Thread.Sleep(500);
        harness.Session.RequestStop();

        Assert.True(harness.WaitForCompletion(TimeSpan.FromSeconds(15)));
        Assert.DoesNotContain(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("got "));
    }

    [Fact]
    public void ReadHost_IsAnsweredByTheSink()
    {
        var path = Write("answer.ps1", """
            $answer = Read-Host 'Say something'
            Write-Output "got $answer"
            """);

        using var harness = new DebugSessionHarness(path);
        harness.Sink.InputAnswers.Enqueue("hello");
        harness.Start([]);

        Assert.True(harness.WaitForCompletion());
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("got hello"));
    }

    [Fact]
    public void Detach_WhileStopped_LetsTheScriptFinish()
    {
        var path = Write("detach.ps1", """
            foreach ($i in 1..3) {
                $last = $i
            }
            Write-Output "finished $last"
            """);

        using var harness = new DebugSessionHarness(path);
        harness.Start([new BreakpointRequest(path, 2, true)]);

        Assert.NotNull(harness.WaitForStop());

        harness.Session.Detach();

        // The breakpoint would hit twice more; detaching drops it rather than stopping the script.
        Assert.True(harness.WaitForCompletion());
        Assert.Null(harness.WaitForStop(TimeSpan.FromMilliseconds(200)));
        Assert.False(harness.LastCompletion!.Faulted);
    }

    [Fact]
    public void Detach_ReleasesAPendingReadHostWithAnEmptyLine()
    {
        var path = Write("detach-read.ps1", """
            $answer = Read-Host 'Say something'
            $Return = "answer=[$answer]"
            """);

        using var harness = new DebugSessionHarness(path);
        harness.Start([], captureResult: (ps, _) => ps.Runspace.SessionStateProxy.PSVariable.GetValue("Return"));

        Assert.True(harness.WaitForState(DebugSessionState.Running, TimeSpan.FromSeconds(20)));
        Thread.Sleep(500);

        harness.Session.Detach();

        Assert.True(harness.WaitForCompletion());
        Assert.Equal("answer=[]", harness.LastCompletion!.Result);
    }

    [Fact]
    public void CaptureResult_ReadsReturnAndRawOutput_InTheGlobalScope()
    {
        var path = Write("result.ps1", """
            param()
            'first'
            [pscustomobject]@{ Name = 'second' }
            $Return = 42
            """);

        using var harness = new DebugSessionHarness(path);

        IReadOnlyList<object?>? raw = null;

        harness.Start([], captureResult: (ps, output) =>
        {
            raw = output;
            return ps.Runspace.SessionStateProxy.PSVariable.GetValue("Return");
        });

        Assert.True(harness.WaitForCompletion());
        Assert.Equal(42, harness.LastCompletion!.Result);
        Assert.NotNull(raw);
        Assert.Equal(2, raw.Count);
    }

    [Fact]
    public void Variables_InjectedByTheBuilder_AreVisibleToTheScript()
    {
        var path = Write("vars.ps1", "Write-Output \"dir=$InstallDirectory type=$ScriptType\"");

        using var harness = new DebugSessionHarness(path);
        harness.Start([], variables: [new PowerShellVariable("InstallDirectory", @"C:\Games\Test", typeof(string))]);

        Assert.True(harness.WaitForCompletion());
        Assert.Contains(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains(@"dir=C:\Games\Test type=Install"));
    }

    [Fact]
    public void Exit_EndsTheScriptWithoutFaulting_AndRecordsTheCode()
    {
        var path = Write("exit.ps1", """
            Write-Output 'before'
            exit 3
            Write-Output 'after'
            """);

        using var harness = new DebugSessionHarness(path);
        harness.Start([]);

        Assert.True(harness.WaitForCompletion());
        Assert.False(harness.LastCompletion!.Faulted, harness.LastCompletion.ErrorMessage);
        Assert.Equal(3, harness.LastCompletion.ExitCode);
        Assert.DoesNotContain(harness.Sink.TextOf(ConsoleOutputKind.Output), t => t.Contains("after"));
    }

    [Fact]
    public void ParseErrors_FaultTheRun_AndAreReported()
    {
        var path = Write("broken.ps1", "if ($true) {");

        using var harness = new DebugSessionHarness(path);
        harness.Start([]);

        Assert.True(harness.WaitForCompletion());
        Assert.True(harness.LastCompletion!.Faulted);
        Assert.NotEmpty(harness.Sink.TextOf(ConsoleOutputKind.Error));
    }
}
