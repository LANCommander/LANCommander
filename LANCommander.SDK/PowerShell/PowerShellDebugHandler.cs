using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell;

public class PowerShellDebugHandler
{
    public Func<System.Management.Automation.PowerShell, Task> OnDebugStart;
    public Func<System.Management.Automation.PowerShell, Task> OnDebugBreak;
    public Func<LogLevel, string, Task> OnOutput;
}