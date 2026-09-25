using System;
using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using LANCommander.Launcher.Controls.ScriptEditor;
using LANCommander.Launcher.ViewModels.ScriptDebugger;

namespace LANCommander.Launcher.Views.ScriptDebugger;

/// <summary>
/// Hosts the text editor and owns the pieces that cannot be expressed in XAML: the breakpoint margin,
/// the two background renderers, the token colorizer and the debounced parser. Switching scripts swaps
/// the document and the margin's breakpoint store underneath them.
/// </summary>
public partial class EditorView : UserControl
{
    private readonly PowerShellColorizer _colorizer = new();
    private readonly ErrorSquiggleRenderer _squiggles = new();
    private readonly CurrentLineRenderer _currentLine = new();
    private readonly DebouncedParser _parser = new(TimeSpan.FromMilliseconds(250));

    private BreakpointMargin? _margin;
    private ScriptDebuggerWindowViewModel? _model;
    private ScriptDocumentViewModel? _document;

    public EditorView()
    {
        InitializeComponent();

        var textView = Editor.TextArea.TextView;
        textView.BackgroundRenderers.Add(_currentLine);
        textView.BackgroundRenderers.Add(_squiggles);
        textView.LineTransformers.Add(_colorizer);

        Editor.TextArea.Caret.PositionChanged += OnCaretChanged;

        _parser.Parsed += OnParsed;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_model is not null)
        {
            _model.NavigateToLineRequested -= NavigateToLine;
            _model.InsertAtCaretRequested -= InsertAtCaret;
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _model = DataContext as ScriptDebuggerWindowViewModel;

        if (_model is null)
            return;

        _model.NavigateToLineRequested += NavigateToLine;
        _model.InsertAtCaretRequested += InsertAtCaret;
        _model.PropertyChanged += OnModelPropertyChanged;

        ShowDocument(_model.SelectedDocument);
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_model is null)
            return;

        switch (e.PropertyName)
        {
            case nameof(ScriptDebuggerWindowViewModel.CurrentLine):
                _currentLine.Line = _model.CurrentLine;

                // Only the background layer changed, so skip the full Redraw that would also re-run the
                // colorizer over every visible line.
                Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
                break;

            case nameof(ScriptDebuggerWindowViewModel.SelectedDocument):
                ShowDocument(_model.SelectedDocument);
                break;
        }
    }

    private void ShowDocument(ScriptDocumentViewModel? document)
    {
        if (ReferenceEquals(document, _document) && _margin is not null)
            return;

        _document = document;

        if (_margin is not null)
        {
            _margin.Detach();
            Editor.TextArea.LeftMargins.Remove(_margin);
            _margin = null;
        }

        Editor.Document = document?.Document ?? new TextDocument();

        if (document is not null)
        {
            _margin = new BreakpointMargin(document.Breakpoints);

            // Index 0 puts the breakpoint gutter to the left of the line numbers, matching VS.
            Editor.TextArea.LeftMargins.Insert(0, _margin);
        }

        _currentLine.Line = _model?.CurrentLine ?? 0;
        _parser.Attach(document?.Document);
    }

    private void OnParsed(SyntaxSnapshot snapshot)
    {
        _colorizer.Update(snapshot);
        _squiggles.Update(snapshot);
        _model?.SetParseErrorCount(snapshot.Errors.Length);

        // Edits can push two breakpoints onto one line; this tick is the natural place to collapse them.
        _document?.Breakpoints.DeduplicateAnchors();

        Editor.TextArea.TextView.Redraw();
    }

    private void OnCaretChanged(object? sender, EventArgs e)
    {
        if (_model is null)
            return;

        var caret = Editor.TextArea.Caret;
        _model.CaretLine = caret.Line;
        _model.CaretText = "Ln " + caret.Line + ", Col " + caret.Column;
    }

    /// <summary>Write a command name into the document at the caret, then hand focus back to the editor.</summary>
    private void InsertAtCaret(string text)
    {
        if (_document is null || string.IsNullOrEmpty(text))
            return;

        Editor.Document.Insert(Editor.CaretOffset, text);
        Editor.Focus();
    }

    private void NavigateToLine(int line)
    {
        if (line < 1 || line > Editor.Document.LineCount)
            return;

        Editor.ScrollToLine(line);
        Editor.TextArea.Caret.Line = line;
        Editor.TextArea.Caret.Column = 1;
    }
}
