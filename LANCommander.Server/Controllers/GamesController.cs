using LANCommander.SDK.Helpers;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Importers;

namespace LANCommander.Server.Controllers
{
    [Authorize(Roles = RoleService.AdministratorRoleName)]
    public class GamesController : BaseController
    {
        private readonly GameService GameService;
        private readonly MediaService MediaService;
        private readonly ArchiveService ArchiveService;
        private readonly ImportContextFactory ImportContextFactory;

        public GamesController(
            ILogger<GamesController> logger,
            GameService gameService,
            MediaService mediaService,
            ArchiveService archiveService,
            ImportContextFactory importContextFactory) : base(logger)
        {
            GameService = gameService;
            MediaService = mediaService;
            ArchiveService = archiveService;
            ImportContextFactory = importContextFactory;
        }

        public async Task ExportAsync(Guid id, ImportRecordFlags flags)
        {
            using (var context = ImportContextFactory.Create())
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

                await context.PrepareGameExportQueueAsync(game, flags);
                await context.ExportQueueAsync(Response.BodyWriter.AsStream());
            }
        }
    }
}
