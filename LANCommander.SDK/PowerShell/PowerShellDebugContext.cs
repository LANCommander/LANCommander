using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell;

public class PowerShellDebugContext(System.Management.Automation.PowerShell ps) : IScriptDebugContext
{
    public Guid SessionId { get; set; } = Guid.NewGuid();

    public async Task ExecuteAsync(string script)
    {
        ps.Commands.Clear();
        ps.AddScript(script);
        
        await ps.InvokeAsync().ConfigureAwait(false);
    }
}