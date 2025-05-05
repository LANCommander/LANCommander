using LANCommander.SDK;
using LANCommander.Server.Interceptors;
using LANCommander.Server.Services;

namespace LANCommander.Server.Startup;

public static class Beacon
{
    public static async Task StartBeaconAsync(this WebApplication app)
    {
        var settings = SettingService.GetSettings();
        var client = app.Services.GetRequiredService<Client>();
        
        client.Beacon.Initialize();
        client.Beacon.AddBeaconMessageInterceptor(new BeaconMessageInterceptor());

        if (settings.Beacon.Enabled)
            await client.Beacon.StartBeaconAsync(settings.Beacon.Port, settings.Beacon.Address, settings.Beacon.Name);
    }
}