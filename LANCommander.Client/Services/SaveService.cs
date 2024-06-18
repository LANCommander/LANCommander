using LANCommander.Client.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class SaveService
    {
        private readonly SDK.Client Client;

        public SaveService(SDK.Client client)
        {
            Client = client;
        }

        public async Task DownloadLatest(Game game, string installDirectory = "")
        {
            if (installDirectory == "")
                installDirectory = game.InstallDirectory;

            await Task.Run(() => Client.Saves.Download(installDirectory, game.Id));
        }
    }
}
