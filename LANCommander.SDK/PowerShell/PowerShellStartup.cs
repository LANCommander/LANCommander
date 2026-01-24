using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Services;
using Microsoft.Extensions.DependencyInjection;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell;

public class PowerShellStartup : PsStartup
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLANCommanderClient<Settings>();
        services.AddScoped<ISteamCmdService, SteamCmdService>();
        services.AddScoped<SteamStoreService>();
    }
}