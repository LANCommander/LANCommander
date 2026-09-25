using System;
using System.ComponentModel;
using System.Management.Automation.Language;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    private const int HoverMaxLines = 20;
    private const int HoverMaxLength = 2000;

    private SyntaxSnapshot _snapshot = SyntaxSnapshot.Empty;
    private int _hoverSequence;
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

        textView.PointerHover += OnPointerHover;
        textView.PointerHoverStopped += (_, _) => CloseHover();
        textView.ScrollOffsetChanged += (_, _) => CloseHover();

        // Tunnel, so Ctrl+wheel zooms before the editor's ScrollViewer takes it as a scroll.
        Editor.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

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
            case nameof(ScriptDebuggerWindowViewModel.State):
                if (!_model.IsStopped)
                    CloseHover();
                break;

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
        _snapshot = snapshot;
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

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_model is null || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0)
            return;

        if (e.Delta.Y > 0)
            _model.ZoomInCommand.Execute(null);
        else
            _model.ZoomOutCommand.Execute(null);

        e.Handled = true;
    }

    /// <summary>While stopped, show the value of the variable under the pointer.</summary>
    private async void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (_model is not { IsStopped: true } || _document is null)
            return;

        var textView = Editor.TextArea.TextView;
        var position = textView.GetPositionFloor(e.GetPosition(textView) + textView.ScrollOffset);

        if (position is null)
            return;

        var offset = Editor.Document.GetOffset(position.Value.Location);
        var expression = FindVariableAt(_snapshot, offset);

        if (expression is null)
            return;

        var sequence = ++_hoverSequence;
        var result = await _model.EvaluateHoverAsync(expression);

        // The pointer moved on, or the script resumed, while the value was on its way.
        if (sequence != _hoverSequence || result is null)
            return;

        var value = result.Output.Trim();

        if (!result.IsError && value.Length == 0)
            value = "$null";

        HoverExpression.Text = expression;
        HoverValue.Text = Truncate(value);
        HoverValue[!TextBlock.ForegroundProperty] = HoverValue.GetResourceObservable(
            result.IsError ? "ErrorTextBrush" : "TextPrimaryBrush").ToBinding();
        HoverPopup.IsOpen = true;
    }

    private void CloseHover()
    {
        _hoverSequence++;
        HoverPopup.IsOpen = false;
    }

    /// <summary>
    /// The variable token covering the offset, as an expression to evaluate. A splat written <c>@name</c>
    /// becomes <c>$name</c>. Looks inside expandable strings so "$path\file" works too.
    /// </summary>
    private static string? FindVariableAt(SyntaxSnapshot snapshot, int offset)
    {
        for (var i = snapshot.FirstIndexAtOrBefore(offset); i < snapshot.Count; i++)
        {
            var token = snapshot[i];

            if (token.Extent.StartOffset > offset)
                break;

            if (token.Extent.EndOffset > offset && FindVariableIn(token, offset) is { } found)
                return found;
        }

        return null;
    }

    private static string? FindVariableIn(Token token, int offset)
    {
        if (token.Extent.StartOffset > offset || token.Extent.EndOffset <= offset)
            return null;

        switch (token)
        {
            case StringExpandableToken { NestedTokens.Count: > 0 } expandable:
                foreach (var nested in expandable.NestedTokens)
                {
                    if (FindVariableIn(nested, offset) is { } found)
                        return found;
                }

                return null;

            case VariableToken { Kind: TokenKind.Variable } variable:
                return variable.Text;

            case VariableToken { Kind: TokenKind.SplattedVariable } splatted:
                return "$" + splatted.Text[1..];

            default:
                return null;
        }
    }

    private static string Truncate(string value)
    {
        var truncated = false;

        if (value.Length > HoverMaxLength)
        {
            value = value[..HoverMaxLength];
            truncated = true;
        }

        var lines = value.Split('\n');

        if (lines.Length > HoverMaxLines)
        {
            value = string.Join('\n', lines[..HoverMaxLines]);
            truncated = true;
        }

        return truncated ? value.TrimEnd() + "\n…" : value;
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
