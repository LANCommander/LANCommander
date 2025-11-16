using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;

using LANCommander.Server.ImportExport.Factories;

namespace LANCommander.Server.Controllers
{
    [Authorize(Roles = RoleService.AdministratorRoleName)]
    public class GamesController : BaseController
    {
        private readonly GameService GameService;
        private readonly ExportContextFactory ExportContextFactory;

        public GamesController(
            ILogger<GamesController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            GameService gameService,
            ExportContextFactory exportContextFactory) : base(logger, settingsProvider)
        {
            GameService = gameService;
            ExportContextFactory = exportContextFactory;
        }

        public async Task ExportAsync(Guid id)
        {
            using (var context = ExportContextFactory.Create())
            {
                var game = await GameService
                    .Include(g => g.Actions)
                    .Include(g => g.Archives)
                    .Include(g => g.BaseGame)
                    .Include(g => g.Categories)
                    .Include(g => g.Collections)
                    .Include(g => g.CustomFields)
                    .Include(g => g.DependentGames)
                    .Include(g => g.Developers)
                    .Include(g => g.Engine)
                    .Include(g => g.Genres)
                    .Include(g => g.Media)
                    .Include(g => g.MultiplayerModes)
                    .Include(g => g.Platforms)
                    .Include(g => g.Publishers)
                    .Include(g => g.Redistributables)
                    .Include(g => g.Scripts)
                    .Include(g => g.Tags)
                    .GetAsync(id);

                if (game == null)
                {
                    Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                
                Response.ContentType = "application/octet-stream";
                Response.Headers.Append("Content-Disposition", @$"attachment; filename=""{game.Title}.lcx""");

                //await context.PrepareGameExportQueueAsync(game, flags);
                await context.ExportQueueAsync(Response.BodyWriter.AsStream());
            }
        }
    }
}
