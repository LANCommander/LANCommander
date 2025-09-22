using LANCommander.SDK.Models;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class TagService(ApiRequestFactory apiRequestFactory)
    {
        public async Task<Tag> CreateAsync(Tag tag)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Tags")
                .AddBody(tag)
                .PostAsync<Tag>();
        }

        public async Task<Tag> UpdateAsync(Tag tag)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Tags/{tag.Id}")
                .AddBody(tag)
                .PostAsync<Tag>();
        }

        public async Task DeleteAsync(Tag tag)
        {
            await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Tags/{tag.Id}")
                .DeleteAsync<object>();
        }
    }
}
