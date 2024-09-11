using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class SaveService : BaseService
    {
        public SaveService(SDK.Client client, ILogger<SaveService> saveService) : base(client, saveService) { }

        public async Task DownloadLatestAsync(string installDirectory, Guid gameId)
        {
            await Task.Run(() => Client.Saves.DownloadAsync(installDirectory, gameId));
        }

        public async Task DownloadLatest(string installDirectory, Guid gameId)
        {

            await Task.Run(() => Client.Saves.DownloadAsync(installDirectory, gameId));
        }

        public async Task UploadAsync(string installDirectory, Guid gameId)
        {
            await Task.Run(() => Client.Saves.UploadAsync(installDirectory, gameId));
        }
    }
}
