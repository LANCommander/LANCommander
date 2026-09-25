using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger.Items;

public sealed partial class WatchItemViewModel : ObservableObject
{
    private readonly DebugSessionController _controller;

    public WatchItemViewModel(string expression, DebugSessionController controller)
    {
        Expression = expression;
        _controller = controller;
    }

    public string Expression { get; }

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private bool _isError;

    public IBrush Foreground => IsError ? ErrorBrush : NormalBrush;

    // The launcher's ErrorText and TextPrimary tokens.
    private static readonly IBrush ErrorBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x78, 0x75));
    private static readonly IBrush NormalBrush = new ImmutableSolidColorBrush(Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF));

    partial void OnIsErrorChanged(bool value) => OnPropertyChanged(nameof(Foreground));

    [ObservableProperty]
    private bool _isEvaluating;

    /// <summary>
    /// Evaluate through the pump. Await, never block: between enqueueing and the pump servicing
    /// it, the script can resume, which faults the task rather than hanging the UI.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_controller.State is not (DebugSessionState.Stopped or DebugSessionState.Evaluating))
        {
            MarkUnavailable();
            return;
        }

        IsEvaluating = true;
        IsError = false;

        try
        {
            var result = await _controller.EvaluateAsync(Expression);
            Value = result.Output.Trim();
            IsError = result.IsError;

            if (!IsError && Value.Length == 0)
                Value = "$null";
        }
        catch (Exception ex)
        {
            Value = ex.GetBaseException().Message;
            IsError = true;
        }
        finally
        {
            IsEvaluating = false;
        }
    }

    public void MarkUnavailable()
    {
        Value = "(not stopped)";
        IsError = false;
        IsEvaluating = false;
    }
}
