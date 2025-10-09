using LANCommander.SDK.Extensions;
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
            using (var op = Logger.BeginDebugOperation("Getting saves for game"))
            {
                op.Enrich("GameId", gameId);
                
                var saves = await client.Saves.GetAsync(gameId);
            
                if (saves == null)
                    saves = new List<GameSave>();
                
                op.Complete();

                return saves;
            }
        }

        public async Task DownloadLatestAsync(string installDirectory, Guid gameId)
        {
            using (var op = Logger.BeginDebugOperation("Downloading latest save"))
            {
                op.Enrich("GameId", gameId);
                op.Enrich("InstallDirectory", installDirectory);
                
                await client.Saves.DownloadAsync(installDirectory, gameId);
                
                op.Complete();
            }
        }

        public async Task DownloadLatest(string installDirectory, Guid gameId)
        {
            using (var op = Logger.BeginDebugOperation("Downloading latest save"))
            {
                op.Enrich("GameId", gameId);
                op.Enrich("InstallDirectory", installDirectory);
                
                await client.Saves.DownloadAsync(installDirectory, gameId);
                
                op.Complete();
            }
        }

        public async Task DownloadAsync(string installDirectory, Guid gameId, Guid saveId)
        {
            using (var op = Logger.BeginDebugOperation("Downloading save"))
            {
                op.Enrich("GameId", gameId);
                op.Enrich("SaveId", saveId);
                op.Enrich("InstallDirectory", installDirectory);
                
                await client.Saves.DownloadAsync(installDirectory, gameId, saveId);
                
                op.Complete();
            }
        }

        public async Task UploadAsync(string installDirectory, Guid gameId)
        {
            using (var op = Logger.BeginDebugOperation("Uploading save"))
            {
                op.Enrich("GameId", gameId);
                op.Enrich("InstallDirectory", installDirectory);
                
                await client.Saves.UploadAsync(installDirectory, gameId);
                
                op.Complete();
            }
        }

        public async Task DeleteAsync(Guid saveId)
        {
            using (var op = Logger.BeginDebugOperation("Deleting save"))
            {
                op.Enrich("SaveId", saveId);
                
                await client.Saves.DeleteAsync(saveId);
                
                op.Complete();
            }
        }
    }
}
 