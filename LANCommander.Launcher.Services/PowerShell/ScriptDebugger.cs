using System.Text.RegularExpressions;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.PowerShell;

public class ScriptDebugger : IScriptDebugger
{
    public Func<IScriptDebugContext, Task>? OnDebugStart;
    public Func<IScriptDebugContext, Task>? OnDebugBreak;
    public Func<IScriptDebugContext, Task>? OnDebugEnd;
    public Func<LogLevel, string, Task>? OnOutput;
    
    private static readonly Regex TokenRegex = new(@"\{[^}]+\}", RegexOptions.Compiled);
    
    public Task StartAsync(IScriptDebugContext context)
    {
        return OnDebugStart is null 
            ? Task.CompletedTask 
            : OnDebugStart(context);
    }

    public Task EndAsync(IScriptDebugContext context)
    {
        return OnDebugEnd is null 
            ? Task.CompletedTask 
            : OnDebugEnd(context);
    }

    public Task BreakAsync(IScriptDebugContext context)
    {
        return OnDebugBreak is null 
            ? Task.CompletedTask 
            : OnDebugBreak(context);
    }

    public Task OutputAsync(IScriptDebugContext context, LogLevel level, string message, params object[] args)
    {
        if (OnOutput is null)
            return Task.CompletedTask;

        return OnOutput(level, Format(message, args));
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