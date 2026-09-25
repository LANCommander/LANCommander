using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using LANCommander.SDK.PowerShell.Debugging;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.ViewModels.ScriptDebugger.Items;
using LANCommander.SDK.PowerShell.Debugging.Hosting;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

public enum ConsoleInputMode
{
    /// <summary>Nothing is running; the input box is read-only.</summary>
    Idle,

    /// <summary>Stopped at a breakpoint; input goes to the debugger as an expression.</summary>
    Stopped,

    /// <summary>The script called Read-Host; input answers that prompt.</summary>
    AwaitingInput,
}

/// <summary>
/// The console panel, and the application's <see cref="IConsoleSink"/>.
/// </summary>
/// <remarks>
/// Every sink method is called on the PS-Pipeline thread and must return immediately, so writes
/// are queued and drained on a timer. That batching is not an optimisation: a tight
/// <c>1..100000 | ForEach-Object { Write-Host $_ }</c> posts tens of thousands of items and would
/// starve the UI thread completely if each one marshalled separately.
/// </remarks>
public sealed partial class ConsoleViewModel : ObservableObject, IConsoleSink, IDisposable
{
    private const int MaxLines = 20_000;
    private const int TrimTo = 15_000;

    private readonly ConcurrentQueue<Chunk> _pending = new();
    private readonly DispatcherTimer _drainTimer;
    private readonly StringBuilder _partialLine = new();

    private TaskCompletionSource<string?>? _inputRequest;
    private CancellationTokenRegistration _inputCancellation;

    public ConsoleViewModel()
    {
        _drainTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _drainTimer.Tick += (_, _) => Drain();
        _drainTimer.Start();
    }

    /// <summary>Assigned after construction: the controller needs this view model as its sink.</summary>
    public DebugSessionController? Controller { get; set; }

    public ObservableCollection<ConsoleLineViewModel> Lines { get; } = new();

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private ConsoleInputMode _inputMode = ConsoleInputMode.Idle;

    [ObservableProperty]
    private string _inputPrompt = string.Empty;

    [ObservableProperty]
    private bool _isSecureInput;

    [ObservableProperty]
    private string _progressText = string.Empty;

    public bool IsInputEnabled => InputMode is not ConsoleInputMode.Idle;

    partial void OnInputModeChanged(ConsoleInputMode value) => OnPropertyChanged(nameof(IsInputEnabled));

    // ---- IConsoleSink (pipeline thread) --------------------------------------------------

    public void Write(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) =>
        _pending.Enqueue(new Chunk(kind, text, NewLine: false));

    public void WriteLine(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) =>
        _pending.Enqueue(new Chunk(kind, text, NewLine: true));

    public void ReportProgress(long sourceId, ScriptProgress progress)
    {
        var text = progress.IsCompleted
            ? string.Empty
            : progress.Activity + (string.IsNullOrEmpty(progress.StatusDescription) ? "" : " - " + progress.StatusDescription);

        Dispatcher.UIThread.Post(() => ProgressText = text);
    }

    public Task<string?> ReadLineAsync(string? prompt, bool secure, CancellationToken cancellationToken)
    {
        // RunContinuationsAsynchronously so completing this from the UI thread does not resume the
        // blocked pipeline continuation inline on the UI thread.
        var request = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.IsCancellationRequested)
        {
            request.TrySetCanceled(cancellationToken);
            return request.Task;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _inputRequest = request;
            InputPrompt = prompt ?? string.Empty;
            IsSecureInput = secure;
            InputMode = ConsoleInputMode.AwaitingInput;

            if (!string.IsNullOrEmpty(prompt))
                AppendLine(ConsoleOutputKind.Prompt, prompt);
        });

        // Cancelling the session token (Shift+F5) has to release a script blocked in Read-Host,
        // or the pipeline never unwinds and the application cannot be closed.
        _inputCancellation.Dispose();
        _inputCancellation = cancellationToken.Register(() =>
        {
            request.TrySetCanceled();
            Dispatcher.UIThread.Post(EndInputPrompt);
        });

        return request.Task;
    }

    // ---- UI thread ----------------------------------------------------------------------

    [RelayCommand]
    private async Task SubmitAsync()
    {
        var text = InputText;
        InputText = string.Empty;

        switch (InputMode)
        {
            case ConsoleInputMode.AwaitingInput:
                AppendLine(ConsoleOutputKind.Echo, IsSecureInput ? "********" : text);
                var request = _inputRequest;
                EndInputPrompt();
                request?.TrySetResult(text);
                break;

            case ConsoleInputMode.Stopped:
                if (string.IsNullOrWhiteSpace(text) || Controller is null)
                    return;

                AppendLine(ConsoleOutputKind.Echo, "[DBG]> " + text);

                var result = await Controller.ExecuteConsoleCommandAsync(text);
                if (!string.IsNullOrEmpty(result.Output))
                    AppendLine(result.IsError ? ConsoleOutputKind.Error : ConsoleOutputKind.Output, result.Output);
                break;

            case ConsoleInputMode.Idle:
                break;
        }
    }

    private void EndInputPrompt()
    {
        _inputRequest = null;
        _inputCancellation.Dispose();
        _inputCancellation = default;
        IsSecureInput = false;
        InputPrompt = string.Empty;
        InputMode = Controller?.State == DebugSessionState.Stopped
            ? ConsoleInputMode.Stopped
            : ConsoleInputMode.Idle;
    }

    /// <summary>Called by the main view model as the session state changes.</summary>
    public void SetDebuggerStopped(bool stopped)
    {
        // A pending Read-Host outranks the debugger prompt; do not steal its input box.
        if (InputMode is ConsoleInputMode.AwaitingInput)
            return;

        InputMode = stopped ? ConsoleInputMode.Stopped : ConsoleInputMode.Idle;
    }

    public void Clear()
    {
        Lines.Clear();
        _partialLine.Clear();
        ProgressText = string.Empty;
    }

    public void AppendLine(ConsoleOutputKind kind, string text) =>
        AddLine(new ConsoleLineViewModel(kind, text));

    /// <summary>Stop draining. Anything the script writes afterwards is dropped with the window.</summary>
    public void Dispose()
    {
        _drainTimer.Stop();
        _inputCancellation.Dispose();
        _inputRequest?.TrySetResult(string.Empty);
    }

    private void Drain()
    {
        if (_pending.IsEmpty)
            return;

        // Bounded per tick so a runaway script cannot monopolise the UI thread; the remainder is
        // picked up on the next tick 50ms later.
        var budget = 2_000;

        while (budget-- > 0 && _pending.TryDequeue(out var chunk))
        {
            if (!chunk.NewLine)
            {
                _partialLine.Append(chunk.Text);
                continue;
            }

            if (_partialLine.Length > 0)
            {
                _partialLine.Append(chunk.Text);
                AddLine(new ConsoleLineViewModel(chunk.Kind, _partialLine.ToString()));
                _partialLine.Clear();
            }
            else
            {
                AddLine(new ConsoleLineViewModel(chunk.Kind, chunk.Text));
            }
        }
    }

    private void AddLine(ConsoleLineViewModel line)
    {
        Lines.Add(line);

        if (Lines.Count <= MaxLines)
            return;

        // Trim in one batch rather than one item per overflow, which would be O(n) removals.
        var excess = Lines.Count - TrimTo;
        for (var i = 0; i < excess; i++)
            Lines.RemoveAt(0);
    }

    private readonly record struct Chunk(ConsoleOutputKind Kind, string Text, bool NewLine);
}
