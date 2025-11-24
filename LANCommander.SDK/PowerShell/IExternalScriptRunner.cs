using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell;

public interface IExternalScriptRunner
{
    Task<bool> ExecuteAsync(PowerShellScript script);
}