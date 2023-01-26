using BeaconLib;
using LANCommander.Models;

namespace LANCommander.Services
{
    public class BeaconService : IHostedService, IDisposable
    {
        private Beacon Beacon;
        private LANCommanderSettings Settings;

        public BeaconService() {
            Settings = SettingService.GetSettings();
            Beacon = new Beacon("LANCommander", Convert.ToUInt16(Settings.Port));
        }

        public void Dispose()
        {
            Beacon?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Beacon.BeaconData = "Acknowledged HQ";
            Beacon.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Beacon.Stop();
            return Task.CompletedTask;
        }
    }
}
