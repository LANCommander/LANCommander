using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>
/// One breakpoint, anchored to the document rather than pinned to a line number.
/// </summary>
/// <remarks>
/// A raw <c>int</c> line rots the instant the user presses Enter above the breakpoint.
/// <see cref="TextAnchor"/> is maintained by AvaloniaEdit through every edit, so
/// <see cref="Line"/> is always current for free.
/// </remarks>
public sealed partial class BreakpointModel : ObservableObject
{
    private TextAnchor _anchor;

    public BreakpointModel(TextAnchor anchor) => _anchor = anchor;

    public TextAnchor Anchor => _anchor;

    /// <summary>
    /// Move onto a fresh anchor. Used when the document's text is replaced wholesale, which would
    /// otherwise carry every anchor to the end of the new text.
    /// </summary>
    internal void Reanchor(TextAnchor anchor)
    {
        _anchor = anchor;
        OnPropertyChanged(nameof(Line));
        OnPropertyChanged(nameof(Display));
    }

    public int Line => _anchor.IsDeleted ? 0 : _anchor.Line;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private int _hitCount;

    /// <summary>
    /// True once the engine has confirmed the breakpoint for the current run. Breakpoints added
    /// while the script is running stay pending (drawn hollow) until the next stop applies them.
    /// </summary>
    [ObservableProperty]
    private bool _isBound;

    /// <summary>The engine's breakpoint id for the current run, if it has one.</summary>
    public int? EngineId { get; set; }

    public string Display => "Line " + Line;
}
