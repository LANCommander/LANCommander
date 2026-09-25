using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.ScriptDebugging;
using LANCommander.Launcher.ViewModels.ScriptDebugger.Items;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Commands;
using LANCommander.SDK.PowerShell.Debugging.Hosting;
using LANCommander.SDK.PowerShell.Debugging.Remote;
using LANCommander.SDK.PowerShell.Debugging.Scripting;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>
/// One game's script debugger window. Lists every script the game can run, edits them, and, while
/// open, catches any of them that runs (during an install, launch, uninstall, or a direct run from
/// here) and stops it at its breakpoints.
/// </summary>
/// <remarks>
/// Everything here runs on the UI thread except the <see cref="IScriptDebugTarget"/> members, which
/// are called on whichever thread is about to run a script. Those read only immutable snapshots and
/// thread-safe collections, and hand anything else to the UI thread with Post.
/// </remarks>
public sealed partial class ScriptDebuggerWindowViewModel : ViewModelBase, IScriptDebugTarget, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly DebugSessionController _controller = new();
    private readonly CommandCatalogService _commandCatalog;
    private readonly ConcurrentDictionary<ScriptKey, ScriptDocumentViewModel> _documents = new();
    private readonly IDisposable? _registration;
    private readonly ScriptDebugPipeServer? _pipeServer;

    private volatile ScriptWorkspace? _workspace;
    private volatile IReadOnlySet<Guid> _ownerIds;
    private int _attached;
    private volatile bool _stepIntoNextRun;
    private ScriptKey? _runningKey;
    private ScriptType? _runningType;
    private int _stoppedLine;
    private string _stoppedSourceMessage = string.Empty;
    private volatile bool _disposed;

    public ScriptDebuggerWindowViewModel(IServiceProvider services, Guid gameId)
    {
        _services = services;
        _logger = services.GetRequiredService<ILogger<ScriptDebuggerWindowViewModel>>();

        GameId = gameId;
        _ownerIds = new HashSet<Guid> { gameId };

        Console = new ConsoleViewModel { Controller = _controller };
        Variables = new VariablesViewModel(_controller);
        CallStack = new CallStackViewModel();
        Watch = new WatchViewModel(_controller);

        var builder = new PowerShellRunspaceBuilder(services, _logger);

        _commandCatalog = new CommandCatalogService(_controller, new CommandCatalog(() =>
        {
            var runspace = builder.Open();
            builder.ImportModules(runspace);
            return runspace;
        }));

        Commands = new CommandsViewModel(_commandCatalog);

        CallStack.FrameSelected += OnFrameSelected;
        Commands.InsertRequested += text => InsertAtCaretRequested?.Invoke(text);

        _controller.SessionBound += OnSessionBound;
        _controller.Stopped += OnStopped;
        _controller.Resumed += OnResumed;
        _controller.StateChanged += OnSessionStateChanged;
        _controller.BreakpointChanged += OnBreakpointChanged;
        _controller.RunCompleted += OnRunCompleted;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                _pipeServer = new ScriptDebugPipeServer(this, _logger);
                _pipeServer.Start();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not start the script debugger pipe; elevated scripts will run without the debugger");
            }
        }

        if (services.GetService<ScriptDebugBroker>() is { } broker)
            _registration = broker.Register(this);
    }

    public Guid GameId { get; }

    public ConsoleViewModel Console { get; }

    public VariablesViewModel Variables { get; }

    public CallStackViewModel CallStack { get; }

    public WatchViewModel Watch { get; }

    public CommandsViewModel Commands { get; }

    public ObservableCollection<ScriptOwnerViewModel> Owners { get; } = new();

    /// <summary>Raised when the editor should scroll to and highlight a line.</summary>
    public event Action<int>? NavigateToLineRequested;

    /// <summary>Raised when text should be written into the document at the caret.</summary>
    public event Action<string>? InsertAtCaretRequested;

    /// <summary>Raised when a script stops, so the window can come to the front.</summary>
    public event Action? ActivateRequested;

    [ObservableProperty]
    private string _title = "Script Debugger";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkspaceMessage))]
    private string? _workspaceMessage;

    public bool HasWorkspaceMessage => !string.IsNullOrEmpty(WorkspaceMessage);

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private object? _selectedTreeItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument))]
    [NotifyPropertyChangedFor(nameof(CanUpload))]
    [NotifyPropertyChangedFor(nameof(CanDiscardDraft))]
    [NotifyPropertyChangedFor(nameof(CanRunDirectly))]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    private ScriptDocumentViewModel? _selectedDocument;

    [ObservableProperty]
    private BreakpointsViewModel? _breakpoints;

    public bool HasDocument => SelectedDocument is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStopped))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(CanStep))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanRunDirectly))]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    private DebugSessionState _state = DebugSessionState.Idle;

    /// <summary>The line the debugger is stopped on in the open document, or 0.</summary>
    [ObservableProperty]
    private int _currentLine;

    [ObservableProperty]
    private string _statusText = "Waiting for a script to run";

    [ObservableProperty]
    private string _caretText = "Ln 1, Col 1";

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private string _sourceUnavailableMessage = string.Empty;

    [ObservableProperty]
    private bool _isCommandsPaneVisible;

    public const double DefaultEditorFontSize = 14;
    public const double MinEditorFontSize = 8;
    public const double MaxEditorFontSize = 40;

    /// <summary>The editor's font size, changed with Ctrl +/- or Ctrl and the scroll wheel.</summary>
    [ObservableProperty]
    private double _editorFontSize = DefaultEditorFontSize;

    /// <summary>The debug panel on show: Variables, Watch, Call Stack or Breakpoints.</summary>
    [ObservableProperty]
    private int _selectedPanelIndex;

    /// <summary>Shown while a script runs in an elevated process, e.g. "Elevated (PID 1234)".</summary>
    [ObservableProperty]
    private string? _remoteSessionText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpload))]
    private bool _isAdministrator;

    [ObservableProperty]
    private bool _hasUploadConflict;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingDrafts))]
    private int _pendingDraftCount;

    public bool HasPendingDrafts => PendingDraftCount > 0;

    public bool IsStopped => State is DebugSessionState.Stopped or DebugSessionState.Evaluating;

    public bool IsIdle => State is DebugSessionState.Idle;

    public bool CanStep => IsStopped;

    public bool CanStop => State is not DebugSessionState.Idle;

    public bool CanRunDirectly => IsIdle && SelectedDocument?.Entry.CanRunDirectly == true && _workspace?.InstallDirectory is not null;

    /// <summary>F5 does something: continues from a stop, or runs the selected script.</summary>
    public bool CanRun => IsStopped || CanRunDirectly;

    public bool CanUpload => IsAdministrator && SelectedDocument?.Entry.ServerScriptId is not null;

    public bool CanDiscardDraft => SelectedDocument?.Entry.Source == ScriptSource.Draft;

    // ---- loading ------------------------------------------------------------------------

    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            using var scope = _services.CreateScope();

            var workspaceService = scope.ServiceProvider.GetRequiredService<ScriptDebugWorkspaceService>();
            var workspace = await workspaceService.LoadAsync(GameId);

            ApplyWorkspace(workspace);

            PendingDraftCount = workspaceService.GetPendingDrafts(workspace).Count;

            try
            {
                var authentication = scope.ServiceProvider.GetService<AuthenticationService>();
                var connection = scope.ServiceProvider.GetService<IConnectionClient>();

                IsAdministrator = authentication?.CanManageGames() == true
                    && connection?.IsConnected() == true
                    && connection.IsOfflineMode() == false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not determine whether the user can manage games");
                IsAdministrator = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load scripts for game {GameId}", GameId);
            WorkspaceMessage = "Could not load this game's scripts: " + ex.GetBaseException().Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Show a freshly loaded workspace, keeping open documents and the selection.</summary>
    public void ApplyWorkspace(ScriptWorkspace workspace)
    {
        _workspace = workspace;
        _ownerIds = new HashSet<Guid>(workspace.OwnerIds) { GameId };

        Title = $"Script Debugger - {workspace.Title}";
        IsInstalled = workspace.IsInstalled;
        WorkspaceMessage = workspace.Message ?? (workspace.Owners.Count == 0 ? "This game has no scripts the launcher runs." : null);

        var selectedKey = (SelectedTreeItem as ScriptItemViewModel)?.Key;

        Owners.Clear();

        foreach (var owner in workspace.Owners)
            Owners.Add(new ScriptOwnerViewModel(owner));

        foreach (var entry in workspace.Scripts)
        {
            if (!_documents.TryGetValue(entry.Key, out var document))
                continue;

            // Clean documents follow their source, e.g. from the server copy to the installed file
            // once the game is installed. Edited ones keep the user's text.
            var sourceChanged = document.Entry.Source != entry.Source || document.Entry.LocalPath != entry.LocalPath;

            document.Entry = entry;

            if (sourceChanged && !document.IsDirty)
                document.Load(ReadEntry(entry));
        }

        foreach (var item in Owners.SelectMany(o => o.Scripts))
        {
            if (_documents.TryGetValue(item.Key, out var document))
                item.IsDirty = document.IsDirty;

            item.IsRunning = _runningKey == item.Key;
        }

        var select = Owners.SelectMany(o => o.Scripts).FirstOrDefault(s => s.Key == selectedKey)
            ?? Owners.SelectMany(o => o.Scripts).FirstOrDefault();

        SelectedTreeItem = select;

        OnPropertyChanged(nameof(CanRunDirectly));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(CanUpload));
    }

    partial void OnSelectedTreeItemChanged(object? value)
    {
        if (value is ScriptItemViewModel item)
            SelectedDocument = GetOrOpenDocument(item.Entry);
    }

    partial void OnSelectedDocumentChanged(ScriptDocumentViewModel? oldValue, ScriptDocumentViewModel? newValue)
    {
        Breakpoints = newValue is null ? null : new BreakpointsViewModel(newValue.Breakpoints);

        if (Breakpoints is not null)
            Breakpoints.NavigateRequested += line => NavigateToLineRequested?.Invoke(line);

        HasUploadConflict = false;

        // The halted line belongs to the running script; switching back to it brings the highlight back.
        var showsStop = newValue is not null && newValue.Key == _runningKey && IsStopped;

        CurrentLine = showsStop ? _stoppedLine : 0;
        SourceUnavailableMessage = showsStop ? _stoppedSourceMessage : string.Empty;
    }

    private ScriptDocumentViewModel GetOrOpenDocument(ScriptEntry entry)
    {
        if (_documents.TryGetValue(entry.Key, out var existing))
            return existing;

        var document = new ScriptDocumentViewModel(entry, ReadEntry(entry));

        document.Breakpoints.BreakpointToggled += (_, e) => OnBreakpointToggled(document, e);
        document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScriptDocumentViewModel.IsDirty))
                UpdateTreeItem(document.Key, item => item.IsDirty = document.IsDirty);
        };

        _documents[entry.Key] = document;

        return document;
    }

    private string ReadEntry(ScriptEntry entry)
    {
        try
        {
            if (entry.Source == ScriptSource.Installed && entry.LocalPath is not null)
                return File.ReadAllText(entry.LocalPath);

            if (File.Exists(entry.DraftPath))
                return File.ReadAllText(entry.DraftPath);

            return entry.ServerContents ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read {Script}", entry.Name);
            return string.Empty;
        }
    }

    private void UpdateTreeItem(ScriptKey key, Action<ScriptItemViewModel> update)
    {
        foreach (var item in Owners.SelectMany(o => o.Scripts).Where(s => s.Key == key))
            update(item);
    }

    // ---- editing ------------------------------------------------------------------------

    [RelayCommand]
    private async Task SaveAsync()
    {
        var document = SelectedDocument;

        if (document is null)
            return;

        try
        {
            using var scope = _services.CreateScope();

            var entry = await scope.ServiceProvider
                .GetRequiredService<ScriptDebugWorkspaceService>()
                .SaveAsync(GameId, document.Entry, document.Document.Text);

            document.MarkSaved(entry);
            UpdateTreeItem(document.Key, item => item.Entry = entry);

            OnPropertyChanged(nameof(CanDiscardDraft));

            StatusText = entry.Source == ScriptSource.Draft
                ? "Saved as a draft. It is written into the game when this script first runs."
                : "Saved";
        }
        catch (Exception ex)
        {
            StatusText = "Could not save: " + ex.GetBaseException().Message;
        }
    }

    [RelayCommand]
    private void DiscardDraft()
    {
        var document = SelectedDocument;

        if (document is null || document.Entry.Source != ScriptSource.Draft)
            return;

        using var scope = _services.CreateScope();

        var entry = scope.ServiceProvider.GetRequiredService<ScriptDebugWorkspaceService>().DiscardDraft(GameId, document.Entry);

        document.MarkSaved(entry);
        document.Load(entry.ServerContents ?? string.Empty);
        UpdateTreeItem(document.Key, item => item.Entry = entry);

        OnPropertyChanged(nameof(CanDiscardDraft));
        StatusText = "Draft discarded";
    }

    [RelayCommand]
    private Task UploadAsync() => UploadCoreAsync(overwrite: false);

    [RelayCommand]
    private Task OverwriteUploadAsync() => UploadCoreAsync(overwrite: true);

    [RelayCommand]
    private void CancelUpload() => HasUploadConflict = false;

    private async Task UploadCoreAsync(bool overwrite)
    {
        var document = SelectedDocument;

        if (document is null || !CanUpload)
            return;

        HasUploadConflict = false;

        var text = document.Document.Text;

        try
        {
            using var scope = _services.CreateScope();

            await scope.ServiceProvider
                .GetRequiredService<ScriptDebugWorkspaceService>()
                .UploadAsync(document.Entry, text, overwrite ? null : document.BaseContents);

            document.MarkUploaded(text);
            StatusText = $"Uploaded {document.Entry.Name} to the server";
        }
        catch (ScriptConflictException)
        {
            HasUploadConflict = true;
            StatusText = "The server's copy changed after this script was opened";
        }
        catch (Exception ex)
        {
            StatusText = "Could not upload: " + ex.GetBaseException().Message;
        }
    }

    [RelayCommand]
    private void ApplyPendingDrafts()
    {
        var workspace = _workspace;

        if (workspace is null)
            return;

        using var scope = _services.CreateScope();

        var workspaceService = scope.ServiceProvider.GetRequiredService<ScriptDebugWorkspaceService>();

        foreach (var entry in workspaceService.GetPendingDrafts(workspace))
        {
            workspaceService.ApplyDraft(GameId, entry);

            if (_documents.TryGetValue(entry.Key, out var document) && !document.IsDirty)
                document.Load(ReadEntry(entry));
        }

        PendingDraftCount = 0;
        StatusText = "Drafts applied to the installed scripts";
    }

    [RelayCommand]
    private void DiscardPendingDrafts()
    {
        var workspace = _workspace;

        if (workspace is null)
            return;

        foreach (var entry in workspace.Scripts)
            ScriptDrafts.Discard(GameId, entry.Key);

        PendingDraftCount = 0;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    public void SetParseErrorCount(int count) => ErrorCount = count;

    // ---- debugging ----------------------------------------------------------------------

    /// <summary>F5: continue from a stop, or run the selected script.</summary>
    [RelayCommand]
    private void Run()
    {
        if (IsStopped)
        {
            _controller.Resume(DebugResumeKind.Continue);
            return;
        }

        RunSelected(stepIntoOnStart: false);
    }

    /// <summary>F11: step into from a stop, or run the selected script breaking on its first line.</summary>
    [RelayCommand]
    private void StepInto()
    {
        if (IsStopped)
        {
            _controller.Resume(DebugResumeKind.StepInto);
            return;
        }

        RunSelected(stepIntoOnStart: true);
    }

    [RelayCommand]
    private void StepOver()
    {
        if (IsStopped)
            _controller.Resume(DebugResumeKind.StepOver);
    }

    [RelayCommand]
    private void StepOut()
    {
        if (IsStopped)
            _controller.Resume(DebugResumeKind.StepOut);
    }

    [RelayCommand]
    private void Stop() => _controller.RequestStop();

    [RelayCommand]
    private void ToggleBreakpoint() => SelectedDocument?.Breakpoints.Toggle(CaretLine);

    [RelayCommand]
    private void ToggleCommandsPane() => IsCommandsPaneVisible = !IsCommandsPaneVisible;

    [RelayCommand]
    private void ZoomIn() => EditorFontSize = Math.Min(MaxEditorFontSize, EditorFontSize + 1);

    [RelayCommand]
    private void ZoomOut() => EditorFontSize = Math.Max(MinEditorFontSize, EditorFontSize - 1);

    [RelayCommand]
    private void ResetZoom() => EditorFontSize = DefaultEditorFontSize;

    /// <summary>
    /// Run the selected script on its own, the same way the launcher would: through ScriptClient, with
    /// the variables it gets in production, elevated if it needs to be. This window attaches to it like
    /// any other run.
    /// </summary>
    private void RunSelected(bool stepIntoOnStart)
    {
        var document = SelectedDocument;
        var installDirectory = _workspace?.InstallDirectory;

        if (!IsIdle || document is null)
            return;

        if (!document.Entry.CanRunDirectly || installDirectory is null)
        {
            StatusText = document.Entry.Type == ScriptType.RunWrapper
                ? "RunWrapper scripts run when the game is played."
                : installDirectory is null
                    ? "Install the game to run its scripts. Breakpoints set now are hit during installation."
                    : $"Install {document.Entry.OwnerName} to run its scripts. Breakpoints set now are hit during installation.";
            return;
        }

        _stepIntoNextRun = stepIntoOnStart;

        var entry = document.Entry;

        StatusText = $"Running {entry.Name}...";

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _services.CreateScope();

                var result = await scope.ServiceProvider
                    .GetRequiredService<ScriptDebugRunner>()
                    .RunAsync(entry, GameId, installDirectory);

                Dispatcher.UIThread.Post(() =>
                    Console.AppendLine(ConsoleOutputKind.System, $"[debugger] {entry.Name} returned {result ?? "nothing"}"));
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Console.AppendLine(ConsoleOutputKind.Error, $"[debugger] {entry.Name} could not be run: {ex.GetBaseException().Message}");
                    StatusText = "Run failed";
                });
            }
            finally
            {
                _stepIntoNextRun = false;
            }
        });
    }

    // ---- IScriptDebugTarget (script threads) ----------------------------------------------

    public ScriptDebugRemoteEndpoint? RemoteEndpoint => _pipeServer?.Endpoint;

    /// <summary>
    /// A script belongs to this window if its owner is this game or one of its addons,
    /// redistributables or tools, and it is running for this game. A redistributable shared by two
    /// games therefore only stops in the window of the game it is running for.
    /// </summary>
    public bool Matches(ScriptIdentity identity)
    {
        if (_disposed)
            return false;

        var owners = _ownerIds;

        if (!owners.Contains(identity.Key.OwnerId))
            return false;

        if (identity.GameId is { } gameId)
            return owners.Contains(gameId);

        // Tool scripts outside a game carry no game id; fall back to where they run.
        var installDirectory = _workspace?.InstallDirectory;

        return installDirectory is null
            || identity.InstallDirectory is null
            || ScriptPath.AreSame(installDirectory, identity.InstallDirectory);
    }

    /// <summary>
    /// Called just before the script file is read. Unsaved edits are written into it, or failing that a
    /// draft saved before the game was installed, so what runs is what this window shows.
    /// </summary>
    public void PrepareScriptFile(ScriptIdentity identity, string path)
    {
        try
        {
            _documents.TryGetValue(identity.Key, out var document);

            if (document?.DirtyText is { } text)
            {
                if (document.Entry.Source == ScriptSource.Installed)
                    File.WriteAllText(path, text);
                else
                    ScriptDrafts.WriteScriptFile(path, text, document.Entry.RequiresAdmin);

                ScriptDrafts.Discard(GameId, identity.Key);

                Dispatcher.UIThread.Post(() =>
                {
                    document.MarkWrittenTo(path);
                    UpdateTreeItem(identity.Key, item => item.Entry = document.Entry);
                    Console.AppendLine(ConsoleOutputKind.System, $"[debugger] Wrote unsaved edits to {path}");
                });

                return;
            }

            var requiresAdmin = document?.Entry.RequiresAdmin
                ?? _workspace?.Scripts.FirstOrDefault(s => s.Key == identity.Key)?.RequiresAdmin
                ?? false;

            if (ScriptDrafts.TryPromote(GameId, identity.Key, path, requiresAdmin))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (document is not null)
                    {
                        document.MarkWrittenTo(path);
                        UpdateTreeItem(identity.Key, item => item.Entry = document.Entry);
                    }

                    Console.AppendLine(ConsoleOutputKind.System, $"[debugger] Wrote the saved draft to {path}");
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write edits to {Path}", path);
        }
    }

    public ScriptDebugAttachment? BeginAttach(ScriptIdentity identity)
    {
        if (_disposed)
            return null;

        // One run at a time. A second script that starts meanwhile runs without the debugger.
        if (Interlocked.CompareExchange(ref _attached, 1, 0) != 0)
        {
            Console.WriteLine(ConsoleOutputKind.System,
                $"[debugger] A {identity.Key.Type} script ran without the debugger because another script is being debugged.");
            return null;
        }

        IReadOnlyList<BreakpointRequest> breakpoints = _documents.TryGetValue(identity.Key, out var document)
            ? document.Breakpoints.Published
            : [];

        var stepIntoOnStart = _stepIntoNextRun;
        _stepIntoNextRun = false;

        return new ScriptDebugAttachment
        {
            Sink = Console,
            Breakpoints = breakpoints.Select(b => b with { ScriptPath = identity.ScriptPath ?? string.Empty }).ToArray(),
            StepIntoOnStart = stepIntoOnStart,
            SessionStarted = session =>
            {
                // Posted ahead of Bind so the window has the script open before the first stop arrives.
                Dispatcher.UIThread.Post(() => OnSessionStarting(identity));
                _controller.Bind(session);
            },
        };
    }

    // ---- session events (UI thread) --------------------------------------------------------

    private void OnSessionStarting(ScriptIdentity identity)
    {
        _runningKey = identity.Key;
        _runningType = identity.Key.Type;

        var entry = _workspace?.Scripts.FirstOrDefault(s => s.Key == identity.Key)
            ?? new ScriptEntry
            {
                Key = identity.Key,
                OwnerKind = identity.OwnerKind,
                OwnerName = identity.OwnerKind.ToString(),
                Name = identity.Key.Type.ToString(),
                Source = ScriptSource.Installed,
                LocalPath = identity.ScriptPath,
                DraftPath = ScriptDrafts.GetPath(GameId, identity.Key),
            };

        var document = GetOrOpenDocument(entry);

        // The run uses the installed file even if this window still shows the server copy (the game
        // was installed after the window opened).
        if (identity.ScriptPath is not null && document.Entry.Source != ScriptSource.Installed)
        {
            document.MarkWrittenTo(identity.ScriptPath);

            if (File.Exists(identity.ScriptPath))
                document.Load(File.ReadAllText(identity.ScriptPath));
        }

        document.RunningPath = identity.ScriptPath;
        document.Breakpoints.MarkAllUnbound();

        UpdateTreeItem(identity.Key, item => item.IsRunning = true);

        var item = Owners.SelectMany(o => o.Scripts).FirstOrDefault(s => s.Key == identity.Key);

        if (item is not null)
            SelectedTreeItem = item;
        else
            SelectedDocument = document;

        Variables.Clear();
        CallStack.Clear();
        Watch.MarkUnavailable();
        SourceUnavailableMessage = string.Empty;

        Console.AppendLine(ConsoleOutputKind.System, $"> {entry.OwnerName}: {entry.Name}");
    }

    private void OnSessionBound(IDebugSessionHandle session)
    {
        RemoteSessionText = session.IsRemote
            ? $"Elevated (PID {session.ProcessId})"
            : null;
    }

    private ScriptDocumentViewModel? RunningDocument =>
        _runningKey is { } key && _documents.TryGetValue(key, out var document) ? document : null;

    private void OnStopped(DebuggerStopInfo info)
    {
        CallStack.Show(info.Frames);

        var document = RunningDocument;

        if (document is not null && SelectedDocument != document)
            SelectedDocument = document;

        if (document is not null && ScriptPath.AreSame(info.ScriptName, document.RunningPath))
        {
            CurrentLine = info.LineNumber;
            SourceUnavailableMessage = string.Empty;
            NavigateToLineRequested?.Invoke(info.LineNumber);
        }
        else
        {
            // No source for this frame: a dynamic ScriptBlock, Invoke-Expression, a module function, or
            // a script this one dot-sourced. Fall back to the nearest frame in the running script.
            var nearest = info.Frames.FirstOrDefault(f => ScriptPath.AreSame(f.ScriptName, document?.RunningPath));

            CurrentLine = nearest?.LineNumber ?? 0;
            SourceUnavailableMessage = "No source available for "
                + (info.Frames.FirstOrDefault()?.FunctionName ?? "this frame")
                + (nearest is null ? "." : " - showing the nearest frame in this script.");

            if (nearest is not null)
                NavigateToLineRequested?.Invoke(nearest.LineNumber);
        }

        _stoppedLine = CurrentLine;
        _stoppedSourceMessage = SourceUnavailableMessage;

        Console.SetDebuggerStopped(true);
        StatusText = CurrentLine > 0 ? "Stopped at line " + CurrentLine : "Stopped";

        _ = Watch.RefreshAllAsync();

        ActivateRequested?.Invoke();
    }

    private void OnResumed()
    {
        ClearStop();
        Variables.Clear();
        CallStack.Clear();
        Watch.MarkUnavailable();
        Console.SetDebuggerStopped(false);
    }

    private void ClearStop()
    {
        _stoppedLine = 0;
        _stoppedSourceMessage = string.Empty;
        CurrentLine = 0;
        SourceUnavailableMessage = string.Empty;
    }

    /// <summary>
    /// Evaluate an expression the user is hovering over in the editor, in the scope the script is stopped
    /// in. Null when not stopped, or when the script resumed before the answer came back.
    /// </summary>
    public async Task<EvaluationResult?> EvaluateHoverAsync(string expression)
    {
        if (!IsStopped)
            return null;

        try
        {
            return await _controller.EvaluateAsync(expression);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not evaluate {Expression} for a hover", expression);
            return null;
        }
    }

    private async void OnFrameSelected(CallStackFrameViewModel? frame)
    {
        if (frame is null)
        {
            Variables.Clear();
            return;
        }

        Variables.Show(await _controller.GetFrameVariablesAsync(frame.Info.Index));

        if (frame.Info.ScriptName is not null
            && ScriptPath.AreSame(frame.Info.ScriptName, RunningDocument?.RunningPath)
            && frame.Info.LineNumber > 0)
        {
            NavigateToLineRequested?.Invoke(frame.Info.LineNumber);
        }
    }

    private void OnSessionStateChanged(DebugSessionState state)
    {
        State = state;

        StatusText = state switch
        {
            DebugSessionState.Idle => "Waiting for a script to run",
            DebugSessionState.Starting => "Starting...",
            DebugSessionState.Running => "Running",
            DebugSessionState.Stopped => CurrentLine > 0 ? "Stopped at line " + CurrentLine : "Stopped",
            DebugSessionState.Evaluating => "Evaluating...",
            DebugSessionState.Stopping => "Stopping...",
            _ => StatusText,
        };
    }

    /// <summary>
    /// The catalogue opens its own runspace and walks PSModulePath, so it is only built once someone
    /// actually looks at the pane.
    /// </summary>
    partial void OnIsCommandsPaneVisibleChanged(bool value)
    {
        if (value && !_commandsLoaded)
        {
            _commandsLoaded = true;
            _ = Commands.LoadAsync();
        }
    }

    private bool _commandsLoaded;

    partial void OnStateChanged(DebugSessionState value)
    {
        // Offer the live refresh rather than taking it: a Get-Command round trip on every stop would
        // put a multi-second query on the critical path of every step.
        Commands.SetSessionAvailable(IsStopped);
    }

    private void OnBreakpointToggled(ScriptDocumentViewModel document, BreakpointChangedEventArgs e)
    {
        if (document.RunningPath is { } path && _controller.IsBusy && _runningKey == document.Key)
            _controller.ChangeBreakpoint(new BreakpointRequest(path, e.Line, e.Enabled), e.Added);
    }

    private void OnBreakpointChanged(BreakpointUpdate update)
    {
        var model = RunningDocument?.Breakpoints.Items.FirstOrDefault(b => b.Line == update.Line);

        if (model is null)
            return;

        model.EngineId = update.BreakpointId;
        model.HitCount = update.HitCount;
        model.IsBound = update.UpdateType is not BreakpointUpdateType.Removed;
    }

    private void OnRunCompleted(RunCompletion completion)
    {
        var document = RunningDocument;
        var type = _runningType;

        ClearStop();
        State = DebugSessionState.Idle;
        RemoteSessionText = null;
        Console.SetDebuggerStopped(false);

        if (document is not null)
            document.RunningPath = null;

        if (_runningKey is { } key)
            UpdateTreeItem(key, item => item.IsRunning = false);

        _runningKey = null;
        _runningType = null;

        var suffix = completion.ExitCode is { } code ? " (exit code " + code + ")" : string.Empty;
        var seconds = completion.Duration.TotalSeconds.ToString("0.00");

        Console.AppendLine(
            completion.Faulted ? ConsoleOutputKind.Error : ConsoleOutputKind.System,
            completion.Faulted
                ? $"< Failed after {seconds}s{suffix}{(completion.ErrorMessage is null ? string.Empty : ": " + completion.ErrorMessage)}"
                : $"< Finished in {seconds}s{suffix}");

        StatusText = completion.Faulted ? "Finished with errors" : "Waiting for a script to run";

        Interlocked.Exchange(ref _attached, 0);

        _ = Commands.ResetToCatalogAsync();

        // Installing or uninstalling moves scripts between the server and the install directory.
        if (type is ScriptType.Install or ScriptType.Uninstall || !IsInstalled)
            _ = LoadAsync();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Let a script parked at a breakpoint finish rather than leave an install hanging forever.
        _controller.Detach();
        _registration?.Dispose();

        if (_pipeServer is not null)
            _ = _pipeServer.DisposeAsync().AsTask();

        _commandCatalog.Dispose();
        Console.Dispose();
    }
}
