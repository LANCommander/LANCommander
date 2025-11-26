using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell;

public interface IScriptDebugger
{
    Task StartAsync(System.Management.Automation.PowerShell ps);
    Task EndAsync(System.Management.Automation.PowerShell ps);
    Task BreakAsync(System.Management.Automation.PowerShell ps);
    Task OutputAsync(LogLevel level, string message, params object[] args);
}