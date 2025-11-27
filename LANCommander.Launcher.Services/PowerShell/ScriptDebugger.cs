using System.Text.RegularExpressions;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.PowerShell;

public class ScriptDebugger : IScriptDebugger
{
    public Func<System.Management.Automation.PowerShell, Task> OnDebugStart;
    public Func<System.Management.Automation.PowerShell, Task> OnDebugBreak;
    public Func<System.Management.Automation.PowerShell, Task> OnDebugEnd;
    public Func<LogLevel, string, Task> OnOutput;
    
    private static readonly Regex TokenRegex = new(@"\{[^}]+\}", RegexOptions.Compiled);
    
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
        await OnOutput?.Invoke(level, Format(message, args))!;
    }

    private string Format(string template, object?[] args)
    {
        if (args == null || args.Length == 0)
            return template;
        
        int i = 0;
        
        return TokenRegex.Replace(template, _ => 
            i < args.Length ? args[i++]?.ToString() ?? string.Empty : string.Empty);
    }
}