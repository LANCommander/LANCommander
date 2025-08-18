using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class TagService
    {
        private readonly ILogger _logger;

        private readonly Client _client;

        public TagService(Client client)
        {
            _client = client;
        }

        public TagService(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<Tag> CreateAsync(Tag tag)
        {
            return await _client.PostRequestAsync<Tag>("/api/Tags", tag);
        }

        public async Task<Tag> UpdateAsync(Tag tag)
        {
            return await _client.PostRequestAsync<Tag>($"/api/Tags/{tag.Id}", tag);
        }

        public async Task DeleteAsync(Tag tag)
        {
            await _client.DeleteRequestAsync<Tag>($"/api/Tags/{tag.Id}");
        }
    }
}
