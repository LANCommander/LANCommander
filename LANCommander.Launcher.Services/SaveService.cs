using LANCommander.Launcher.Data.Models;
using LANCommander.SDK.Models;
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

        public async Task<IEnumerable<GameSave>> Get(Guid gameId)
        {
            return await Client.Saves.GetAsync(gameId);
        }

        public async Task DownloadLatestAsync(string installDirectory, Guid gameId)
        {
            await Client.Saves.DownloadAsync(installDirectory, gameId);
        }

        public async Task DownloadLatest(string installDirectory, Guid gameId)
        {

            await Client.Saves.DownloadAsync(installDirectory, gameId);
        }

        public async Task DownloadAsync(string installDirectory, Guid gameId, Guid saveId)
        {
            await Client.Saves.DownloadAsync(installDirectory, gameId, saveId);
        }

        public async Task UploadAsync(string installDirectory, Guid gameId)
        {
            await Client.Saves.UploadAsync(installDirectory, gameId);
        }

        public async Task DeleteAsync(Guid saveId)
        {
            await Client.Saves.DeleteAsync(saveId);
        }
    }
}
 