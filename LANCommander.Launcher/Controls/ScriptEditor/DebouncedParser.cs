using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using LANCommander.SDK.PowerShell.Debugging.Scripting;

namespace LANCommander.Launcher.Controls.ScriptEditor;

/// <summary>
/// Re-parses the document a short pause after typing stops, off the UI thread, and publishes the
/// result to the colorizer and the squiggle renderer.
/// </summary>
public sealed class DebouncedParser : IDisposable
{
    private readonly DispatcherTimer _timer;
    private TextDocument? _document;
    private int _generation;

    public DebouncedParser(TimeSpan delay)
    {
        _timer = new DispatcherTimer { Interval = delay };
        _timer.Tick += OnTick;
    }

    /// <summary>Raised on the UI thread with each completed parse.</summary>
    public event Action<SyntaxSnapshot>? Parsed;

    public void Attach(TextDocument? document)
    {
        if (_document is not null)
            _document.Changed -= OnDocumentChanged;

        _document = document;

        if (_document is not null)
            _document.Changed += OnDocumentChanged;

        // Parse immediately so a freshly opened file is coloured before the first keystroke.
        ParseNow();
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        // Restart on every keystroke: parsing a 2000-line script takes single-digit milliseconds,
        // so the delay exists to avoid parsing 60 times a second while typing, not because the
        // parse is slow.
        _timer.Stop();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        ParseNow();
    }

    private async void ParseNow()
    {
        var document = _document;
        if (document is null)
        {
            Parsed?.Invoke(SyntaxSnapshot.Empty);
            return;
        }

        // CreateSnapshot must happen on the UI thread: TextDocument is explicitly not thread-safe.
        // What it returns is immutable and safe to read from anywhere.
        var text = document.CreateSnapshot().Text;
        var generation = ++_generation;

        var snapshot = await Task.Run(() =>
        {
            var result = PowerShellParseService.Parse(text);
            return new SyntaxSnapshot(result.Tokens, result.Errors);
        }).ConfigureAwait(true);

        // A fast parse started later can finish first. Only the newest result may win.
        if (generation != _generation)
            return;

        Parsed?.Invoke(snapshot);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;

        if (_document is not null)
            _document.Changed -= OnDocumentChanged;

        _document = null;
    }
}
