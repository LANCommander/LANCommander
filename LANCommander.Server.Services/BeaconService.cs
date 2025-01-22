﻿using BeaconLib;
using LANCommander.Server.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class BeaconService : BaseService, IHostedService, IDisposable
    {
        private Beacon Beacon;

        public BeaconService(ILogger<BeaconService> logger) : base(logger)
        {
            Beacon = new Beacon("LANCommander", Convert.ToUInt16(Settings.Port));
        }

        public void Dispose()
        {
            Beacon?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            string[] dataParts = new string[]
            {
                Settings.Beacon?.Address,
                Settings.Beacon?.Name,
                UpdateService.GetCurrentVersion().ToString(),
            };

            Beacon.BeaconData = String.Join('|', dataParts);
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
