using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class SaveService(
        ILogger<SaveService> logger,
        SDK.Client client) : BaseService(logger)
    {
        public async Task<IEnumerable<GameSave>> Get(Guid gameId)
        {
            var saves = await client.Saves.GetAsync(gameId);
            
            if (saves == null)
                saves = new List<GameSave>();

            return saves;
        }

        public async Task DownloadLatestAsync(string installDirectory, Guid gameId)
        {
            await client.Saves.DownloadAsync(installDirectory, gameId);
        }

        public async Task DownloadLatest(string installDirectory, Guid gameId)
        {

            await client.Saves.DownloadAsync(installDirectory, gameId);
        }

        public async Task DownloadAsync(string installDirectory, Guid gameId, Guid saveId)
        {
            await client.Saves.DownloadAsync(installDirectory, gameId, saveId);
        }

        public async Task UploadAsync(string installDirectory, Guid gameId)
        {
            await client.Saves.UploadAsync(installDirectory, gameId);
        }

        public async Task DeleteAsync(Guid saveId)
        {
            await client.Saves.DeleteAsync(saveId);
        }
    }
}
 