using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class SaveService(
        ILogger<SaveService> logger,
        SaveClient saveClient) : BaseService(logger)
    {
        public async Task<IEnumerable<GameSave>> Get(Guid gameId)
        {
            using (var op = Logger.BeginDebugOperation("Getting saves for game"))
            {
                op.Enrich("GameId", gameId);
                
                var saves = await saveClient.GetAsync(gameId);
            
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
                
                await saveClient.DownloadAsync(installDirectory, gameId);
                
                op.Complete();
            }
        }

        public async Task DownloadLatest(string installDirectory, Guid gameId)
        {
            using (var op = Logger.BeginDebugOperation("Downloading latest save"))
            {
                op.Enrich("GameId", gameId);
                op.Enrich("InstallDirectory", installDirectory);
                
                await saveClient.DownloadAsync(installDirectory, gameId);
                
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
                
                await saveClient.DownloadAsync(installDirectory, gameId, saveId);
                
                op.Complete();
            }
        }

        public async Task UploadAsync(string installDirectory, Guid gameId)
        {
            using (var op = Logger.BeginDebugOperation("Uploading save"))
            {
                op.Enrich("GameId", gameId);
                op.Enrich("InstallDirectory", installDirectory);
                
                await saveClient.UploadAsync(installDirectory, gameId);
                
                op.Complete();
            }
        }

        public async Task DeleteAsync(Guid saveId)
        {
            using (var op = Logger.BeginDebugOperation("Deleting save"))
            {
                op.Enrich("SaveId", saveId);
                
                await saveClient.DeleteAsync(saveId);
                
                op.Complete();
            }
        }
    }
}
 