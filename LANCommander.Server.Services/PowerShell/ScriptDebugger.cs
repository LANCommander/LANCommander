using System.Text.RegularExpressions;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.PowerShell;

public class ScriptDebugger : IScriptDebugger
{
    public Func<IScriptDebugContext, Task>? OnStart;
    public Func<IScriptDebugContext, Task>? OnBreak;
    public Func<IScriptDebugContext, Task>? OnEnd;
    public Func<IScriptDebugContext, LogLevel, string, Task>? OnOutput;
    
    private IDictionary<Guid, IScriptDebugContext?> _contexts = new Dictionary<Guid, IScriptDebugContext?>();
    
    private static readonly Regex TokenRegex = new(@"\{[^}]+\}", RegexOptions.Compiled);

    public Guid CreateSession()
    {
        var sessionId = Guid.NewGuid();
        
        _contexts.Add(sessionId, null);
        
        return sessionId;
    }
    
    public Task StartAsync(IScriptDebugContext context)
    {
        var latestSession = _contexts.FirstOrDefault(kvp => kvp.Value == null);
        
        context.SessionId = latestSession.Key;
        _contexts[latestSession.Key] = context;
        
        return OnStart is null
            ? Task.CompletedTask
            : OnStart(context);
    }

    public Task EndAsync(IScriptDebugContext context)
    {
        _contexts.Remove(context.SessionId);
        
        return OnEnd is null
            ? Task.CompletedTask
            : OnEnd(context);
    }

    public Task BreakAsync(IScriptDebugContext context)
    {
        return OnBreak is null
            ? Task.CompletedTask
            : OnBreak(context);
    }

    public Task OutputAsync(IScriptDebugContext context, LogLevel level, string message, params object[] args)
    {
        if (OnOutput is null)
            return Task.CompletedTask;
        
        return OnOutput(context, level, Format(message, args));
    }

    public async Task ExecuteAsync(Guid sessionId, string input)
    {
        if (_contexts.ContainsKey(sessionId))
            await _contexts[sessionId]!.ExecuteAsync(input);
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