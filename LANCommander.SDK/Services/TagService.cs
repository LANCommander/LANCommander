using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class TagService
    {
        private readonly ILogger Logger;

        private readonly Client Client;

        public TagService(Client client)
        {
            Client = client;
        }

        public TagService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<Tag> CreateAsync(Tag tag)
        {
            return await Client.PostRequestAsync<Tag>("/api/Tags", tag);
        }

        public async Task<Tag> UpdateAsync(Tag tag)
        {
            return await Client.PostRequestAsync<Tag>($"/api/Tags/{tag.Id}", tag);
        }

        public async Task DeleteAsync(Tag tag)
        {
            await Client.DeleteRequestAsync<Tag>($"/api/Tags/{tag.Id}");
        }
    }
}
