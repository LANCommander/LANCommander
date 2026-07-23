using LANCommander.SDK;
using LANCommander.SDK.Interceptors;
using LANCommander.SDK.Services;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;

namespace LANCommander.Server.Startup;

public static class Beacon
{
    public static async Task StartBeaconAsync(this WebApplication app)
    {
        var settingsProvider = app.Services.GetRequiredService<SettingsProvider<Settings.Settings>>();
        var beaconService = app.Services.GetRequiredService<BeaconClient>();
        var interceptor = app.Services.GetRequiredService<IBeaconMessageInterceptor>();
        var election = app.Services.GetRequiredService<ICoordinatorElection>();

        beaconService.Initialize();
        beaconService.AddBeaconMessageInterceptor(interceptor);

        if (!await election.TryAcquireAsync())
            return;

        if (settingsProvider.CurrentValue.Server.Beacon.Enabled)
            await beaconService.StartBeaconAsync(settingsProvider.CurrentValue.Server.Beacon.Port, settingsProvider.CurrentValue.Server.Beacon.Address, settingsProvider.CurrentValue.Server.Beacon.Name);
    }
}