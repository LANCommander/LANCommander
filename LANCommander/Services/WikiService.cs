using LANCommander.Data.Models;
using LANCommander.PCGamingWiki;
using Razor.Templating.Core;
using System.Reflection;
using System.Text;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace LANCommander.Services
{
    public class WikiService : IDisposable
    {
        private GameService GameService;
        private RedistributableService RedistributableService;
        private WikiClient WikiClient;
        private WikiSite WikiSite;

        public WikiService(GameService gameService, RedistributableService redistributableService)
        {
            GameService = gameService;
            RedistributableService = redistributableService;
            WikiClient = new WikiClient();
        }

        public async Task AuthenticateAsync(string username, string password)
        {
            WikiSite = new WikiSite(WikiClient, "https://lancommander.app/api.php");

            await WikiSite.Initialization;

            await WikiSite.LoginAsync(username, password);
        }

        public async Task<WikiPage> GetPage(Game game)
        {
            var page = new WikiPage(WikiSite, game.Title);

            await page.RefreshAsync(PageQueryOptions.FetchContent);

            return page;
        }

        public async Task<WikiPage> GetPage(Redistributable redistributable)
        {
            var page = new WikiPage(WikiSite, redistributable.Name);

            await page.RefreshAsync(PageQueryOptions.FetchContent);

            return page;
        }

        public async Task PublishPage(WikiPage page, string summary, bool minor = false)
        {
            await page.UpdateContentAsync(summary, minor);
        }

        public string GetUrl(WikiPage page)
        {
            if (page == null)
                return "";

            return $"https://lancommander.app/index.php/{page.PageStub.Title}";
        }

        public async Task<string> GenerateRedistributablePage(Guid id)
        {
            var redistributable = await RedistributableService.Get(id);

            var result = await RazorTemplateEngine.RenderAsync("~/Templates/Wiki/Redistributable.cshtml", redistributable);

            return result.Trim();
        }

        public async Task<string> GenerateGamePage(Guid id)
        {
            var game = await GameService.Get(id);

            var result = await RazorTemplateEngine.RenderAsync("~/Templates/Wiki/Game.cshtml", game);

            return result.Trim();
        }

        public async void Dispose()
        {
            await WikiSite.LogoutAsync();
            WikiClient.Dispose();
        }
    }
}
