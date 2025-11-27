using System;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Options;

namespace LANCommander.SDK.PowerShell;

public class PowerShellScriptFactory(IServiceProvider serviceProvider, IOptions<Settings> settings)
{
    public PowerShellScript Create(ScriptType type) => new(serviceProvider, type, settings);
}