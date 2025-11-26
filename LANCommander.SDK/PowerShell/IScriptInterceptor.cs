using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell;

public interface IScriptInterceptor
{
    Task<bool> ExecuteAsync(PowerShellScript script);
}