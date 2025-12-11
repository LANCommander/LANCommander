using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell;

public interface IScriptDebugger
{
    Task StartAsync(IScriptDebugContext context);
    Task EndAsync(IScriptDebugContext context);
    Task BreakAsync(IScriptDebugContext context);
    Task OutputAsync(IScriptDebugContext context, LogLevel level, string message, params object[] args);
}