using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LANCommander.Launcher.Services.ScriptDebugging;
using LANCommander.Launcher.ViewModels.ScriptDebugger;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// The debugger window's view model driving a real script run: the script attaches through the broker,
/// its stop lands on the UI thread, and continuing lets it finish with the right result.
/// </summary>
public sealed class ScriptDebuggerWindowViewModelTests : IDisposable
{
    private readonly string _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-debugger-vm-{Guid.NewGuid():N}");
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly ServiceProvider _services;

    public ScriptDebuggerWindowViewModelTests()
    {
        Directory.CreateDirectory(_installDirectory);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();
        services.AddSingleton<ScriptDebugBroker>();
        services.AddSingleton<IScriptDebugBroker>(sp => sp.GetRequiredService<ScriptDebugBroker>());

        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();

        try { Directory.Delete(_installDirectory, recursive: true); }
        catch (Exception) { }
    }

    [AvaloniaFact]
    public void ScriptRunForTheGame_StopsInTheWindow_AndContinues()
    {
        var path = ScriptHelper.GetScriptFilePath(_installDirectory, _gameId, ScriptType.Install);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "$first = 1\n$second = $first + 1\n$Return = $second * 21");

        using var debugger = new ScriptDebuggerWindowViewModel(_services, _gameId);

        debugger.ApplyWorkspace(Workspace(path));
        debugger.SelectedDocument!.Breakpoints.Toggle(2);

        var run = Task.Run(() => Script(path).ExecuteAsync<int>());

        PumpUntil(() => debugger.IsStopped);

        Assert.Equal(2, debugger.CurrentLine);
        Assert.NotEmpty(debugger.CallStack.Frames);
        Assert.True(debugger.Owners[0].Scripts[0].IsRunning);

        debugger.RunCommand.Execute(null);

        PumpUntil(() => run.IsCompleted && debugger.IsIdle);

        Assert.Equal(42, run.Result);
        Assert.Equal(0, debugger.CurrentLine);
        Assert.False(debugger.Owners[0].Scripts[0].IsRunning);
        Assert.Contains(debugger.Console.Lines, l => l.Text.StartsWith("< Finished"));
    }

    [AvaloniaFact]
    public void UnsavedEdits_AreWhatRuns()
    {
        var path = ScriptHelper.GetScriptFilePath(_installDirectory, _gameId, ScriptType.Install);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "$Return = 1");

        using var debugger = new ScriptDebuggerWindowViewModel(_services, _gameId);

        debugger.ApplyWorkspace(Workspace(path));
        debugger.SelectedDocument!.Document.Text = "$Return = 2";

        Assert.True(debugger.SelectedDocument.IsDirty);

        var run = Task.Run(() => Script(path).ExecuteAsync<int>());

        PumpUntil(() => run.IsCompleted);

        Assert.Equal(2, run.Result);
        Assert.Equal("$Return = 2", File.ReadAllText(path));

        PumpUntil(() => !debugger.SelectedDocument.IsDirty);
    }

    [AvaloniaFact]
    public void ClosingTheWindow_WhileStopped_LetsTheScriptFinish()
    {
        var path = ScriptHelper.GetScriptFilePath(_installDirectory, _gameId, ScriptType.Install);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "$Return = 5");

        var debugger = new ScriptDebuggerWindowViewModel(_services, _gameId);

        debugger.ApplyWorkspace(Workspace(path));
        debugger.SelectedDocument!.Breakpoints.Toggle(1);

        var run = Task.Run(() => Script(path).ExecuteAsync<int>());

        PumpUntil(() => debugger.IsStopped);

        debugger.Dispose();

        PumpUntil(() => run.IsCompleted);

        Assert.Equal(5, run.Result);
    }

    private ScriptWorkspace Workspace(string path)
    {
        var key = new ScriptKey(_gameId, ScriptType.Install);

        return new ScriptWorkspace(_gameId, "Test Game", _installDirectory, true,
        [
            new ScriptOwnerNode(ScriptOwnerKind.Game, _gameId, "Test Game", false,
            [
                new ScriptEntry
                {
                    Key = key,
                    OwnerKind = ScriptOwnerKind.Game,
                    OwnerName = "Test Game",
                    Name = "Install",
                    Source = ScriptSource.Installed,
                    LocalPath = path,
                    DraftPath = Path.Combine(_installDirectory, "no-drafts", "Install.ps1"),
                },
            ]),
        ]);
    }

    private PowerShellScript Script(string path) =>
        new PowerShellScript(_services, ScriptType.Install, Options.Create(new SdkSettings()))
            .AddVariable("InstallDirectory", _installDirectory)
            .AddVariable("GameManifest", new ManifestGame { Id = _gameId })
            .UseWorkingDirectory(_installDirectory)
            .UseFile(path);

    /// <summary>Run the UI thread's queue until the condition holds; session events arrive by Post.</summary>
    private static void PumpUntil(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(60))
                throw new TimeoutException("Timed out waiting for the debugger.");

            Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(10);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }
}
