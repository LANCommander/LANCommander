using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Factories;

public class ScriptFactory(ScriptService scriptService)
{
    public PowerShellScript Create(ScriptType type)
    {
        return new PowerShellScript(type, scriptService);
    }
}