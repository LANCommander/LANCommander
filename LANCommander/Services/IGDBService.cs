using IGDB;
using IGDB.Models;

namespace LANCommander.Services
{
    public class IGDBService
    {
        private readonly SettingService SettingService;
        private const string DefaultFields = "*";
        private IGDBClient Client;

        public bool Authenticated = false;

        private string ClientId { get; set; }
        private string ClientSecret { get; set; }

        public IGDBService(SettingService settingService)
        {
            SettingService = settingService;

            Authenticate();
        }

        public void Authenticate()
        {
            var settings = SettingService.GetSettings();

            ClientId = settings.IGDBClientId;
            ClientSecret = settings.IGDBClientSecret;

            try
            {
                if (String.IsNullOrWhiteSpace(ClientId) || String.IsNullOrWhiteSpace(ClientSecret))
                    throw new Exception("Invalid IGDB credentials");

                Client = new IGDBClient(ClientId, ClientSecret);
                Authenticated = true;
            }
            catch (Exception ex)
            {
                Authenticated = false;
            }
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
