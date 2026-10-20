using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using LANCommander.Launcher.Views.ScriptDebugger;
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

    [AvaloniaTheory]
    [InlineData("$a = 1\n$b = 2\n$c = 3\n$d = 4")]
    [InlineData("$a = 1\n$b = 20\n$c = 3\n$d = 4\n$e = 5")]
    public void ReloadingTheText_KeepsBreakpointsOnTheirLines(string reloaded)
    {
        var document = new ScriptDocumentViewModel(Entry(ScriptType.Install, "unused"), "$a = 1\n$b = 2\n$c = 3\n$d = 4");

        var breakpoint = document.Breakpoints.Toggle(2)!;
        breakpoint.Enabled = false;

        document.Load(reloaded);

        Assert.Equal(2, breakpoint.Line);
        Assert.False(breakpoint.Enabled);
        Assert.Single(document.Breakpoints.Items);
        Assert.Equal(2, document.Breakpoints.Published.Single().Line);
    }

    [AvaloniaFact]
    public void SwitchingScripts_WhileStopped_KeepsTheHaltedLine()
    {
        var path = ScriptHelper.GetScriptFilePath(_installDirectory, _gameId, ScriptType.Install);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "$first = 1\n$second = $first + 1\n$Return = $second * 21");

        using var debugger = new ScriptDebuggerWindowViewModel(_services, _gameId);

        debugger.ApplyWorkspace(Workspace(path, Entry(ScriptType.Uninstall, Path.Combine(_installDirectory, "Uninstall.ps1"))));
        debugger.SelectedDocument!.Breakpoints.Toggle(2);

        var run = Task.Run(() => Script(path).ExecuteAsync<int>());

        PumpUntil(() => debugger.IsStopped);

        var install = debugger.Owners[0].Scripts[0];
        var uninstall = debugger.Owners[0].Scripts[1];

        debugger.SelectedTreeItem = uninstall;
        Assert.Equal(0, debugger.CurrentLine);

        debugger.SelectedTreeItem = install;
        Assert.Equal(2, debugger.CurrentLine);

        debugger.RunCommand.Execute(null);

        PumpUntil(() => run.IsCompleted && debugger.IsIdle);
    }

    [AvaloniaFact]
    public void CtrlPlusMinusAndCtrlWheel_ChangeTheEditorFontSize()
    {
        var path = ScriptHelper.GetScriptFilePath(_installDirectory, _gameId, ScriptType.Install);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", Enumerable.Range(1, 200).Select(i => $"$line{i} = {i}")));

        using var debugger = new ScriptDebuggerWindowViewModel(_services, _gameId);
        debugger.ApplyWorkspace(Workspace(path));

        var window = new ScriptDebuggerWindow { DataContext = debugger };
        window.Show();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().Single();
        editor.Focus();
        Dispatcher.UIThread.RunJobs();

        var start = ScriptDebuggerWindowViewModel.DefaultEditorFontSize;
        Assert.Equal(start, editor.FontSize);

        window.KeyPress(Key.OemPlus, RawInputModifiers.Control, PhysicalKey.Equal, "=");
        window.KeyPress(Key.Add, RawInputModifiers.Control, PhysicalKey.NumPadAdd, "+");
        Assert.Equal(start + 2, editor.FontSize);

        window.KeyPress(Key.OemMinus, RawInputModifiers.Control, PhysicalKey.Minus, "-");
        Assert.Equal(start + 1, editor.FontSize);

        // Over the editor, Ctrl+wheel zooms and doesn't scroll; a plain wheel still scrolls.
        var centre = editor.TranslatePoint(new Point(editor.Bounds.Width / 2, editor.Bounds.Height / 2), window)!.Value;
        var scroll = editor.TextArea.TextView.ScrollOffset;

        window.MouseWheel(centre, new Vector(0, 1), RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(start + 2, editor.FontSize);
        Assert.Equal(scroll, editor.TextArea.TextView.ScrollOffset);

        window.MouseWheel(centre, new Vector(0, -1), RawInputModifiers.Control);
        window.MouseWheel(centre, new Vector(0, -1), RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(start, editor.FontSize);

        window.MouseWheel(centre, new Vector(0, -1), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(start, editor.FontSize);
        Assert.True(editor.TextArea.TextView.ScrollOffset.Y > scroll.Y);

        // Clamped at both ends, and Ctrl+0 puts it back.
        for (var i = 0; i < 100; i++)
            debugger.ZoomOutCommand.Execute(null);

        Assert.Equal(ScriptDebuggerWindowViewModel.MinEditorFontSize, editor.FontSize);

        window.KeyPress(Key.D0, RawInputModifiers.Control, PhysicalKey.Digit0, "0");
        Assert.Equal(start, editor.FontSize);

        window.Close();
    }

    [Fact]
    public void ScriptItems_ShowTheNameAndTheTypeSeparately()
    {
        var item = new ScriptItemViewModel(Entry(ScriptType.BeforeStart, "unused") with { Name = "Mount ISO" });

        Assert.Equal("Mount ISO", item.Name);
        Assert.Equal("Before Start", item.TypeLabel);
    }

    private ScriptWorkspace Workspace(string path, params ScriptEntry[] others)
    {
        return new ScriptWorkspace(_gameId, "Test Game", _installDirectory, true,
        [
            new ScriptOwnerNode(ScriptOwnerKind.Game, _gameId, "Test Game", false,
            [
                Entry(ScriptType.Install, path),
                ..others,
            ]),
        ]);
    }

    private ScriptEntry Entry(ScriptType type, string path) => new()
    {
        Key = new ScriptKey(_gameId, type),
        OwnerKind = ScriptOwnerKind.Game,
        OwnerName = "Test Game",
        Name = type.ToString(),
        Source = ScriptSource.Installed,
        LocalPath = path,
        DraftPath = Path.Combine(_installDirectory, "no-drafts", type + ".ps1"),
    };

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
