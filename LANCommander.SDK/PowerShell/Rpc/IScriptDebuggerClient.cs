using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell.Rpc;

public interface IScriptDebuggerClient
{
    Task Start(IScriptDebugContext context);
    Task End(IScriptDebugContext context);
    Task Break(IScriptDebugContext context);
    Task Output(IScriptDebugContext context, LogLevel level, string message);
}