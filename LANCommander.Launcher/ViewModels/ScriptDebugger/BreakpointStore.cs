using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using AvaloniaEdit.Document;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>
/// The single source of truth for breakpoints.
/// </summary>
/// <remarks>
/// Lives in the launcher rather than the SDK because it is anchored to the live <see cref="TextDocument"/>, which is
/// UI-thread-only. The engine never sees it: <see cref="Snapshot"/> hands over plain value types.
/// </remarks>
public sealed class BreakpointStore
{
    private TextDocument? _document;

    /// <summary>Bound directly by the Breakpoints panel.</summary>
    public ObservableCollection<BreakpointModel> Items { get; } = new();

    /// <summary>Raised when the set changes, so the margin can repaint.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Raised when a breakpoint is added or removed, so a running session can apply the change.
    /// </summary>
    public event EventHandler<BreakpointChangedEventArgs>? BreakpointToggled;

    /// <summary>
    /// Point the store at a new document. Anchors belong to one document, so they are rebuilt
    /// rather than reused; on New/Open the old breakpoints are simply dropped.
    /// </summary>
    public void AttachDocument(TextDocument? document)
    {
        _document = document;

        foreach (var item in Items)
            item.PropertyChanged -= OnBreakpointPropertyChanged;

        Items.Clear();
        RaiseChanged();
    }

    public BreakpointModel? Find(int line) => Items.FirstOrDefault(b => b.Line == line);

    /// <summary>Add a breakpoint on the line, or remove the one already there.</summary>
    public BreakpointModel? Toggle(int line)
    {
        if (_document is null || line < 1 || line > _document.LineCount)
            return null;

        var existing = Find(line);
        if (existing is not null)
        {
            existing.PropertyChanged -= OnBreakpointPropertyChanged;
            Items.Remove(existing);
            RaiseChanged();
            BreakpointToggled?.Invoke(this, new BreakpointChangedEventArgs(line, existing.Enabled, Added: false));
            return null;
        }

        var anchor = _document.CreateAnchor(_document.GetLineByNumber(line).Offset);

        // SurviveDeletion keeps the anchor usable if the user deletes the line: it migrates to the
        // deletion point, landing the breakpoint on the following line, which is what VS and
        // VS Code do. AfterInsertion pushes it down when text is inserted at the line start.
        anchor.SurviveDeletion = true;
        anchor.MovementType = AnchorMovementType.AfterInsertion;

        var model = new BreakpointModel(anchor);
        model.PropertyChanged += OnBreakpointPropertyChanged;
        Items.Add(model);
        RaiseChanged();
        BreakpointToggled?.Invoke(this, new BreakpointChangedEventArgs(line, model.Enabled, Added: true));
        return model;
    }

    public void Remove(BreakpointModel model)
    {
        if (!Items.Remove(model))
            return;

        model.PropertyChanged -= OnBreakpointPropertyChanged;

        RaiseChanged();
        BreakpointToggled?.Invoke(this, new BreakpointChangedEventArgs(model.Line, model.Enabled, Added: false));
    }

    /// <summary>
    /// The Breakpoints panel binds <c>Enabled</c> two-way straight onto the model, so the change
    /// arrives here rather than through a method call. Without this hook the gutter would not
    /// repaint and a running session would never be told.
    /// </summary>
    private void OnBreakpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BreakpointModel.Enabled) || sender is not BreakpointModel model)
            return;

        RaiseChanged();

        // For the engine, disabling is a removal and re-enabling is a fresh add.
        BreakpointToggled?.Invoke(this, new BreakpointChangedEventArgs(model.Line, model.Enabled, Added: model.Enabled));
    }

    public void Clear()
    {
        foreach (var item in Items)
            item.PropertyChanged -= OnBreakpointPropertyChanged;

        Items.Clear();
        RaiseChanged();
    }

    /// <summary>
    /// Collapse breakpoints that edits have pushed onto the same line, and drop any whose anchor
    /// has been deleted. Called from the parse debounce tick, which already fires after edits.
    /// </summary>
    public void DeduplicateAnchors()
    {
        var seen = new HashSet<int>();
        var doomed = new List<BreakpointModel>();

        foreach (var item in Items)
        {
            var line = item.Line;
            if (line < 1 || !seen.Add(line))
                doomed.Add(item);
        }

        if (doomed.Count == 0)
            return;

        foreach (var item in doomed)
        {
            item.PropertyChanged -= OnBreakpointPropertyChanged;
            Items.Remove(item);
        }

        RaiseChanged();
    }

    /// <summary>
    /// The one-way door into the engine. Must be called on the UI thread (it reads anchors); the result
    /// is value types and is safe to hand to the pipeline thread.
    /// </summary>
    public IReadOnlyList<BreakpointRequest> Snapshot(string scriptPath) =>
        Items.Where(b => b.Line > 0)
             .Select(b => new BreakpointRequest(scriptPath, b.Line, b.Enabled))
             .ToArray();

    private volatile BreakpointRequest[] _published = [];

    /// <summary>
    /// The breakpoints as of the last change, readable from any thread. The script debugger reads this
    /// on the thread that is about to run a script, which must never wait on the UI thread.
    /// </summary>
    public IReadOnlyList<BreakpointRequest> Published => _published;

    /// <summary>
    /// Refresh <see cref="Published"/>. Anchors move with edits without the set changing, so the owning
    /// document calls this after text changes too. UI thread only.
    /// </summary>
    public void Publish() => _published = Snapshot(string.Empty).ToArray();

    private void RaiseChanged()
    {
        Publish();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkAllUnbound()
    {
        foreach (var item in Items)
        {
            item.IsBound = false;
            item.HitCount = 0;
            item.EngineId = null;
        }
    }
}

public sealed record BreakpointChangedEventArgs(int Line, bool Enabled, bool Added);
