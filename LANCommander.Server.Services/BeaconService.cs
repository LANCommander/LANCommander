using BeaconLib;
using LANCommander.Server.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class BeaconService(ILogger<BeaconService> logger) : BaseService(logger), IHostedService, IDisposable
    {
        private Beacon _beacon;

        public override void Initialize()
        {
            _beacon = new Beacon("LANCommander", Convert.ToUInt16(_settings.Port));
        }

        public void Dispose()
        {
            _beacon?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            string[] dataParts = new string[]
            {
                _settings.Beacon?.Address,
                _settings.Beacon?.Name,
                UpdateService.GetCurrentVersion().ToString(),
            };

            _beacon.BeaconData = String.Join('|', dataParts);
            _beacon.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _beacon.Stop();
            return Task.CompletedTask;
        }
    }
}
