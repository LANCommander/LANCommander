using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell;

public class PowerShellDebugHandler
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    
    public event EventHandler<OnDebugStartEventArgs> OnDebugStart;
    public event EventHandler<OnDebugBreakEventArgs> OnDebugBreak;
    public event EventHandler<OnDebugOutputEventArgs> OnDebugOutput;

    private System.Management.Automation.PowerShell _powerShell;

    internal void Start(System.Management.Automation.PowerShell ps = null)
    {
        if (_powerShell == null)
            _powerShell = ps;
        
        OnDebugStart?.Invoke(this,  new OnDebugStartEventArgs
        {
            PowerShell = _powerShell,
        });
    }

    internal void Break(System.Management.Automation.PowerShell ps = null)
    {
        if (_powerShell == null)
            _powerShell = ps;
        
        OnDebugStart?.Invoke(this, new OnDebugStartEventArgs
        {
           PowerShell = _powerShell,
        });
    }

    internal void Output(LogLevel level, string message)
    {
        OnDebugOutput?.Invoke(this, new OnDebugOutputEventArgs
        {
            LogLevel = level,
            Message = message
        });
    }

    public async Task ExecuteAsync(string input)
    {
        if (input.StartsWith('$'))
            input = $"Write-Host {input}";

        _powerShell.Commands.Clear();
        _powerShell.AddScript(input);
        
        await _powerShell.InvokeAsync();
    }
}

public class OnDebugStartEventArgs : EventArgs
{
    public System.Management.Automation.PowerShell PowerShell { get; set; }
}

public class OnDebugBreakEventArgs : EventArgs
{
    public System.Management.Automation.PowerShell PowerShell { get; set; }
}

public class OnDebugOutputEventArgs : EventArgs
{
    public LogLevel LogLevel { get; set; }
    public string Message { get; set; }
}