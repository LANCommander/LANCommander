using System;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Services.ScriptDebugging;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>One script open in the debugger: its text, its breakpoints, and whether it has unsaved edits.</summary>
/// <remarks>
/// The text document and its anchors are UI-thread only. The two members the engine reads from a
/// script thread, <see cref="DirtyText"/> and <see cref="BreakpointStore.Published"/>, are refreshed on
/// every edit so they can be read without touching the document.
/// </remarks>
public sealed partial class ScriptDocumentViewModel : ObservableObject
{
    private volatile string? _dirtyText;
    private bool _loading;

    public ScriptDocumentViewModel(ScriptEntry entry, string text)
    {
        _entry = entry;
        Document = new TextDocument();
        Breakpoints = new BreakpointStore();

        Document.TextChanged += OnTextChanged;

        Load(text);
        Breakpoints.AttachDocument(Document);
    }

    public TextDocument Document { get; }

    public BreakpointStore Breakpoints { get; }

    public ScriptKey Key => Entry.Key;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(SourceLabel))]
    private ScriptEntry _entry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private bool _isDirty;

    /// <summary>The file this script is running from, while a debug session for it is live.</summary>
    [ObservableProperty]
    private string? _runningPath;

    /// <summary>The text as last loaded from or saved to its source. The base for detecting upload conflicts.</summary>
    public string BaseContents { get; private set; } = string.Empty;

    /// <summary>Unsaved text, or null when the document matches its source. Readable from any thread.</summary>
    public string? DirtyText => _dirtyText;

    public string DisplayName => $"{Entry.OwnerName} — {Entry.Name}{(IsDirty ? " *" : string.Empty)}";

    public string SourceLabel => Entry.Source switch
    {
        ScriptSource.Installed => Entry.LocalPath ?? "Installed",
        ScriptSource.Draft => "Draft (not installed yet); written into the game when this script first runs",
        _ => "Server copy (not installed)",
    };

    /// <summary>Replace the text wholesale and treat it as the saved state.</summary>
    public void Load(string text)
    {
        // Starting a run reloads the file it runs from, which is usually what is already on screen.
        if (text != Document.Text)
        {
            _loading = true;

            try
            {
                Breakpoints.ReplaceText(() => Document.Text = text);
                Document.UndoStack.ClearAll();
            }
            finally
            {
                _loading = false;
            }
        }

        BaseContents = text;
        _dirtyText = null;
        IsDirty = false;
        Breakpoints.Publish();
    }

    public void MarkSaved(ScriptEntry entry)
    {
        Entry = entry;
        _dirtyText = null;
        IsDirty = false;
    }

    /// <summary>The debugger wrote this document's text into the file that runs.</summary>
    public void MarkWrittenTo(string path)
    {
        Entry = Entry with { Source = ScriptSource.Installed, LocalPath = path };
        _dirtyText = null;
        IsDirty = false;
    }

    public void MarkUploaded(string text) => BaseContents = text;

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (_loading)
            return;

        _dirtyText = Document.Text;
        IsDirty = true;
        Breakpoints.Publish();
    }
}
