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

        // ── Per-installation tool tracking (canonical) ──────────────────────────────────────
        // A game can now have multiple side-by-side installations, so tool install state can no
        // longer be tracked per game alone — it must be scoped to the exact installation the tool
        // was installed into (GameInstallationTool). The legacy per-game GameTool rows above are
        // kept in sync only for the game's currently *selected* installation, for transitional
        // callers (action bar, tool list) that still read them directly.

        /// <summary>
        /// Returns the install-state join rows for every tool currently installed for the given
        /// installation instance. The related <see cref="Tool"/> is eagerly loaded.
        /// </summary>
        public async Task<List<GameInstallationTool>> GetInstalledToolsForInstallationAsync(Guid installationId)
        {
            return await Context.Set<GameInstallationTool>()
                .Include(git => git.Tool)
                .Where(git => git.GameInstallationId == installationId && git.Installed)
                .ToListAsync();
        }

        public async Task<bool> IsToolInstalledForInstallationAsync(Guid installationId, Guid toolId)
        {
            return await Context.Set<GameInstallationTool>()
                .AnyAsync(git => git.GameInstallationId == installationId && git.ToolId == toolId && git.Installed);
        }

        /// <summary>
        /// Records that a tool has been installed for a specific installation instance. Also
        /// mirrors the change onto the legacy per-game <see cref="GameTool"/> row, but only when
        /// this installation is currently the game's selected one — a tool installed into a
        /// non-selected, pinned side-by-side installation must not appear as installed to
        /// transitional callers that only know about the selected installation.
        /// </summary>
        public async Task SetToolInstalledForInstallationAsync(Guid installationId, Guid gameId, Guid toolId, string installDirectory, string version)
        {
            var installationTool = await Context.Set<GameInstallationTool>()
                .FirstOrDefaultAsync(git => git.GameInstallationId == installationId && git.ToolId == toolId);

            if (installationTool == null)
            {
                installationTool = new GameInstallationTool { GameInstallationId = installationId, ToolId = toolId };
                await Context.Set<GameInstallationTool>().AddAsync(installationTool);
            }

            installationTool.Installed = true;
            installationTool.InstallDirectory = installDirectory;
            installationTool.InstalledVersion = version;
            installationTool.InstalledOn ??= DateTime.Now;

            await Context.SaveChangesAsync();

            if (await IsSelectedInstallationAsync(installationId))
                await SetToolInstalledAsync(gameId, toolId, installDirectory, version);
        }

        /// <summary>
        /// Clears the install state for a tool on a specific installation instance without
        /// removing the installation/tool association. Other installations (of this game or any
        /// other) keep their own independent install state.
        /// </summary>
        public async Task SetToolUninstalledForInstallationAsync(Guid installationId, Guid gameId, Guid toolId)
        {
            var installationTool = await Context.Set<GameInstallationTool>()
                .FirstOrDefaultAsync(git => git.GameInstallationId == installationId && git.ToolId == toolId);

            if (installationTool != null)
            {
                installationTool.Installed = false;
                installationTool.InstallDirectory = null;
                installationTool.InstalledVersion = null;
                installationTool.InstalledOn = null;

                await Context.SaveChangesAsync();
            }

            if (await IsSelectedInstallationAsync(installationId))
                await SetToolUninstalledAsync(gameId, toolId);
        }

        private async Task<bool> IsSelectedInstallationAsync(Guid installationId)
        {
            return await Context.Set<GameInstallation>()
                .AnyAsync(i => i.Id == installationId && i.IsSelected);
        }
    }
}
