using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class ToolService(
        ILogger<ToolService> logger,
        DatabaseContext dbContext) : BaseDatabaseService<Tool>(dbContext, logger)
    {
        /// <summary>
        /// Returns the install-state join rows for every tool currently installed for the given game.
        /// The related <see cref="Tool"/> is eagerly loaded.
        /// </summary>
        public async Task<List<GameTool>> GetInstalledToolsForGameAsync(Guid gameId)
        {
            return await Context.Set<GameTool>()
                .Include(gt => gt.Tool)
                .Where(gt => gt.GameId == gameId && gt.Installed)
                .ToListAsync();
        }

        /// <summary>
        /// Returns the install-state join rows for every game the given tool is installed for.
        /// </summary>
        public async Task<List<GameTool>> GetInstalledGameToolsAsync(Guid toolId)
        {
            return await Context.Set<GameTool>()
                .Where(gt => gt.ToolId == toolId && gt.Installed)
                .ToListAsync();
        }

        public async Task<bool> IsToolInstalledForGameAsync(Guid gameId, Guid toolId)
        {
            return await Context.Set<GameTool>()
                .AnyAsync(gt => gt.GameId == gameId && gt.ToolId == toolId && gt.Installed);
        }

        /// <summary>
        /// Records that a tool has been installed for a specific game. Creates the join row if the
        /// game/tool association does not yet exist locally.
        /// </summary>
        public async Task SetToolInstalledAsync(Guid gameId, Guid toolId, string installDirectory, string version)
        {
            var gameTool = await Context.Set<GameTool>()
                .FirstOrDefaultAsync(gt => gt.GameId == gameId && gt.ToolId == toolId);

            if (gameTool == null)
            {
                gameTool = new GameTool { GameId = gameId, ToolId = toolId };
                await Context.Set<GameTool>().AddAsync(gameTool);
            }

            gameTool.Installed = true;
            gameTool.InstallDirectory = installDirectory;
            gameTool.InstalledVersion = version;
            gameTool.InstalledOn ??= DateTime.Now;

            await Context.SaveChangesAsync();
        }

        /// <summary>
        /// Clears the install state for a tool on a specific game without removing the game/tool
        /// association. Other games keep their own install state.
        /// </summary>
        public async Task SetToolUninstalledAsync(Guid gameId, Guid toolId)
        {
            var gameTool = await Context.Set<GameTool>()
                .FirstOrDefaultAsync(gt => gt.GameId == gameId && gt.ToolId == toolId);

            if (gameTool == null)
                return;

            gameTool.Installed = false;
            gameTool.InstallDirectory = null;
            gameTool.InstalledVersion = null;
            gameTool.InstalledOn = null;

            await Context.SaveChangesAsync();
        }
    }
}
