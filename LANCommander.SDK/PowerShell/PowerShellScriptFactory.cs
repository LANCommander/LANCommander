using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.PowerShell;

public class PowerShellScriptFactory(IServiceProvider serviceProvider)
{
    public PowerShellScript Create(ScriptType type) => new(serviceProvider, type);
}