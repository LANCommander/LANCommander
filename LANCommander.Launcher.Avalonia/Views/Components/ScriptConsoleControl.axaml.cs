using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.Views.Components;

public partial class ScriptConsoleControl : UserControl
{
    public static readonly StyledProperty<bool> IsInputEnabledProperty =
        AvaloniaProperty.Register<ScriptConsoleControl, bool>(nameof(IsInputEnabled), false);

    public bool IsInputEnabled
    {
        get => GetValue(IsInputEnabledProperty);
        set
        {
            SetValue(IsInputEnabledProperty, value);
            InputBorder.IsVisible = value;
        }
    }

    private readonly StringBuilder _outputBuffer = new();
    private IScriptDebugContext? _debugContext;
    private TaskCompletionSource<bool>? _exitTcs;

    public ScriptConsoleControl()
    {
        InitializeComponent();
        
        CommandInput.KeyDown += OnCommandInputKeyDown;
        ExecuteButton.Click += OnExecuteButtonClick;
    }

    /// <summary>
    /// Called when debug session starts
    /// </summary>
    public void OnDebugStart(IScriptDebugContext context)
    {
        _debugContext = context;
        _exitTcs = new TaskCompletionSource<bool>();
        
        Dispatcher.UIThread.Post(() =>
        {
            _outputBuffer.Clear();
            OutputTextBlock.Text = string.Empty;
            IsInputEnabled = false;
        });
    }

    /// <summary>
    /// Called when output is received from the script
    /// </summary>
    public void OnOutput(LogLevel level, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _outputBuffer.AppendLine(message);
            OutputTextBlock.Text = _outputBuffer.ToString();
            
            // Auto-scroll to bottom
            OutputScrollViewer.Offset = new Vector(0, OutputScrollViewer.Extent.Height);
        });
    }

    /// <summary>
    /// Called when script execution completes and enters debug break mode
    /// </summary>
    public async Task OnDebugBreakAsync(IScriptDebugContext context)
    {
        _debugContext = context;
        _exitTcs = new TaskCompletionSource<bool>();
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsInputEnabled = true;
            CommandInput.Focus();
            
            AppendOutput("\n--------- DEBUG MODE ---------");
            AppendOutput("Script execution complete. You can now run commands.");
            AppendOutput("Type 'exit' to close this window.\n");
        });
        
        // Wait for user to type 'exit'
        await _exitTcs.Task;
    }

    /// <summary>
    /// Called when debug session ends
    /// </summary>
    public void OnDebugEnd(IScriptDebugContext context)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsInputEnabled = false;
            AppendOutput("\n--------- SESSION ENDED ---------");
        });
    }

    private void AppendOutput(string text)
    {
        _outputBuffer.AppendLine(text);
        OutputTextBlock.Text = _outputBuffer.ToString();
        OutputScrollViewer.Offset = new Vector(0, OutputScrollViewer.Extent.Height);
    }

    private async void OnCommandInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ExecuteCommandAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CommandInput.Text = string.Empty;
            e.Handled = true;
        }
    }

    private async void OnExecuteButtonClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ExecuteCommandAsync();
    }

    private async Task ExecuteCommandAsync()
    {
        var command = CommandInput.Text?.Trim();
        
        if (string.IsNullOrEmpty(command))
            return;
        
        CommandInput.Text = string.Empty;
        
        // Echo the command
        AppendOutput($"PS> {command}");
        
        if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            _exitTcs?.TrySetResult(true);
            return;
        }
        
        if (_debugContext == null)
        {
            AppendOutput("Error: No active debug context");
            return;
        }
        
        try
        {
            // If command starts with $, wrap it in Write-Host to display the value
            if (command.StartsWith('$'))
                command = "Write-Host " + command;
            
            await _debugContext.ExecuteAsync(command);
        }
        catch (Exception ex)
        {
            AppendOutput($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the console output
    /// </summary>
    public void Clear()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _outputBuffer.Clear();
            OutputTextBlock.Text = string.Empty;
        });
    }
}
