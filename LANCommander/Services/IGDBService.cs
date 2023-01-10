using IGDB;
using IGDB.Models;

namespace LANCommander.Services
{
    public class IGDBService
    {
        private readonly IGDBClient Client;
        private readonly SettingService SettingService;
        private const string DefaultFields = "*";

        public IGDBService(SettingService settingService)
        {
            SettingService = settingService;

            var settings = SettingService.GetSettings();

            Client = new IGDBClient(settings.IGDBClientId, settings.IGDBClientSecret);
        }

        public async Task<Game> Get(long id, params string[] additionalFields)
        {
            var fields = DefaultFields.Split(',').ToList();

            fields.AddRange(additionalFields);

            var games = await Client.QueryAsync<Game>(IGDBClient.Endpoints.Games, $"fields {String.Join(',', fields)}; where id = {id};");

            if (games == null)
                return null;

            return games.FirstOrDefault();
        }

        public async Task<IEnumerable<Game>> Search(string query, params string[] additionalFields)
        {
            var fields = DefaultFields.Split(',').ToList();

            fields.AddRange(additionalFields);

            var games = await Client.QueryAsync<Game>(IGDBClient.Endpoints.Games, $"search \"{query}\"; fields {String.Join(',', fields)};");

            return games.AsEnumerable();
        }
    }
}
