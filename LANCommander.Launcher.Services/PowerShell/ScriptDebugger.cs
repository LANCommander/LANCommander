using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.PowerShell;

public class ScriptDebugger : IScriptDebugger
{
    public Func<System.Management.Automation.PowerShell, Task> OnDebugStart;
    public Func<System.Management.Automation.PowerShell, Task> OnDebugBreak;
    public Func<System.Management.Automation.PowerShell, Task> OnDebugEnd;
    public Func<LogLevel, string, Task> OnOutput;
    
    public async Task StartAsync(System.Management.Automation.PowerShell ps)
    {
        await OnDebugStart?.Invoke(ps)!;
    }

    public async Task EndAsync(System.Management.Automation.PowerShell ps)
    {
        await OnDebugEnd?.Invoke(ps)!;
    }

    public async Task BreakAsync(System.Management.Automation.PowerShell ps)
    {
        await OnDebugBreak?.Invoke(ps)!;
    }

    public async Task OutputAsync(LogLevel level, string message, params object[] args)
    {
        await OnOutput?.Invoke(level, message)!;
    }
}