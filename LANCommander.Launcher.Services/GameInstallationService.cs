using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.SDK.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    /// <summary>
    /// CRUD/selection service for local <see cref="GameInstallation"/> instances — the side-by-side
    /// install records that are replacing the legacy single Game.Installed/InstallDirectory fields
    /// (kept for compatibility; see <see cref="Game.CurrentInstallation"/>). This service owns the
    /// invariants that the database can only partially enforce on its own: install directory
    /// uniqueness across every installation of every game, and at most one selected installation
    /// per game (with <see cref="Game.SelectedInstallationId"/> kept in sync). All mutating
    /// operations run inside a transaction so a partial failure never leaves two installations
    /// selected for the same game or the selection pointer out of sync with the installation rows.
    /// </summary>
    public class GameInstallationService(
        DatabaseContext dbContext,
        ILogger<GameInstallationService> logger) : BaseDatabaseService<GameInstallation>(dbContext, logger)
    {
        /// <summary>
        /// All installation instances for a game, most-recently-installed first with the currently
        /// selected installation always leading.
        /// </summary>
        public async Task<List<GameInstallation>> GetInstallationsForGameAsync(Guid gameId)
        {
            return await Context.Set<GameInstallation>()
                .Where(i => i.GameId == gameId)
                .OrderByDescending(i => i.IsSelected)
                .ThenByDescending(i => i.InstalledOn)
                .ToListAsync();
        }

        public async Task<GameInstallation?> GetSelectedInstallationAsync(Guid gameId)
        {
            return await Context.Set<GameInstallation>()
                .FirstOrDefaultAsync(i => i.GameId == gameId && i.IsSelected);
        }

        public async Task<bool> HasInstallationsAsync(Guid gameId)
        {
            return await Context.Set<GameInstallation>().AnyAsync(i => i.GameId == gameId);
        }

        /// <summary>
        /// Finds the installation of a game whose install directory matches (case-insensitively),
        /// or null if none does. Used to tell whether an Add()/modify request targets an install
        /// that's already on disk (update/modify/move it in place) versus a brand-new/side-by-side
        /// installation (create a new GameInstallation).
        /// </summary>
        public async Task<GameInstallation?> FindByDirectoryAsync(Guid gameId, string? installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
                return null;

            var normalized = NormalizeDirectory(installDirectory);

            var installations = await Context.Set<GameInstallation>()
                .Where(i => i.GameId == gameId)
                .ToListAsync();

            return installations.FirstOrDefault(i => NormalizeDirectory(i.InstallDirectory) == normalized);
        }

        /// <summary>
        /// Finds the installation of a game already pinned to the given archive, or null if no
        /// installation has that exact archive. Used when an explicit archive id is requested so a
        /// repeat request for an archive that's already installed targets that installation instead
        /// of creating a duplicate side-by-side one.
        /// </summary>
        public async Task<GameInstallation?> FindByArchiveAsync(Guid gameId, Guid archiveId)
        {
            return await Context.Set<GameInstallation>()
                .FirstOrDefaultAsync(i => i.GameId == gameId && i.ArchiveId == archiveId);
        }

        /// <summary>
        /// Checks whether a directory is already claimed by any installation (of any game).
        /// Directory comparisons are case-insensitive since install paths are filesystem paths.
        /// </summary>
        public async Task<bool> IsInstallDirectoryInUseAsync(string installDirectory, Guid? excludeInstallationId = null)
        {
            var normalized = NormalizeDirectory(installDirectory);

            var query = Context.Set<GameInstallation>().AsQueryable();

            if (excludeInstallationId.HasValue)
                query = query.Where(i => i.Id != excludeInstallationId.Value);

            var directories = await query.Select(i => i.InstallDirectory).ToListAsync();

            return directories.Any(d => NormalizeDirectory(d) == normalized);
        }

        /// <summary>
        /// Creates a new installation for the given game. Throws if the requested
        /// InstallDirectory is already used by any other installation. The first installation
        /// created for a game is always selected regardless of <paramref name="select"/>, so a
        /// game is never left without a current installation once it has one; subsequent
        /// installations only become selected when explicitly requested.
        /// </summary>
        public async Task<GameInstallation> AddInstallationAsync(GameInstallation installation, bool select = true)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));

            if (string.IsNullOrWhiteSpace(installation.InstallDirectory))
                throw new ArgumentException("InstallDirectory is required.", nameof(installation));

            if (await IsInstallDirectoryInUseAsync(installation.InstallDirectory))
                throw new InvalidOperationException(
                    $"Install directory '{installation.InstallDirectory}' is already used by another installation.");

            await using var transaction = await BeginTransactionIfSupportedAsync();

            var hasExisting = await Context.Set<GameInstallation>().AnyAsync(i => i.GameId == installation.GameId);

            installation.IsSelected = select || !hasExisting;

            if (installation.IsSelected)
                await ClearSelectionAsync(installation.GameId);

            await Context.Set<GameInstallation>().AddAsync(installation);
            await Context.SaveChangesAsync();

            if (installation.IsSelected)
                await PointGameAtSelectedInstallationAsync(installation.GameId, installation.Id);

            if (transaction != null)
                await transaction.CommitAsync();

            return installation;
        }

        /// <summary>
        /// Marks the given installation as selected/active for its game, clearing the selected
        /// flag on any sibling installations and updating Game.SelectedInstallationId to match.
        /// </summary>
        public async Task SelectInstallationAsync(Guid installationId)
        {
            var installation = await Context.Set<GameInstallation>().FindAsync(installationId)
                ?? throw new InvalidOperationException($"Installation '{installationId}' does not exist.");

            await using var transaction = await BeginTransactionIfSupportedAsync();

            await ClearSelectionAsync(installation.GameId);

            installation.IsSelected = true;
            await Context.SaveChangesAsync();

            await PointGameAtSelectedInstallationAsync(installation.GameId, installation.Id);

            if (transaction != null)
                await transaction.CommitAsync();
        }

        /// <summary>
        /// Deletes an installation. If it was the selected installation, another remaining
        /// installation for the same game (the most recently installed one) automatically becomes
        /// selected in its place; if none remain, the game's selection is cleared so
        /// <see cref="Game.CurrentInstallation"/> falls back to null (or the legacy fields, if
        /// still populated).
        /// </summary>
        public async Task DeleteInstallationAsync(Guid installationId)
        {
            var installation = await Context.Set<GameInstallation>().FindAsync(installationId);

            if (installation == null)
                return;

            var gameId = installation.GameId;
            var wasSelected = installation.IsSelected;

            await using var transaction = await BeginTransactionIfSupportedAsync();

            Context.Set<GameInstallation>().Remove(installation);
            await Context.SaveChangesAsync();

            if (wasSelected)
            {
                var replacement = await Context.Set<GameInstallation>()
                    .Where(i => i.GameId == gameId)
                    .OrderByDescending(i => i.InstalledOn)
                    .FirstOrDefaultAsync();

                if (replacement != null)
                {
                    replacement.IsSelected = true;
                    await Context.SaveChangesAsync();

                    await PointGameAtSelectedInstallationAsync(gameId, replacement.Id);
                }
                else
                {
                    await PointGameAtSelectedInstallationAsync(gameId, null);
                }
            }

            if (transaction != null)
                await transaction.CommitAsync();
        }

        /// <summary>
        /// Computes a collision-safe install directory for a new installation of a game. The
        /// first installation for a game keeps its natural/legacy directory exactly as given (so
        /// imports and re-installs of an already-installed game are never silently relocated).
        /// Every additional side-by-side installation gets a sibling directory named after the
        /// base directory with a sanitized version suffix, disambiguated with a numeric suffix if
        /// that name is already taken by any installation.
        /// </summary>
        /// <param name="gameId">The game the new installation is for.</param>
        /// <param name="baseDirectory">
        /// The directory a first/legacy installation of this game would use (e.g. the user's
        /// chosen or default install path for the game).
        /// </param>
        /// <param name="version">Version label used for the sibling suffix, if any.</param>
        /// <param name="reservedDirectories">
        /// Directories already claimed by other in-flight requests that have not yet persisted
        /// their own <see cref="GameInstallation"/> row (e.g. other items still sitting in the
        /// install queue). Without this, two pending installs generated back-to-back — before
        /// either has actually been added to the database — could independently compute the
        /// exact same "collision-safe" sibling directory (most easily triggered by two requests
        /// with the same, or both blank, version) and collide once both eventually run. Callers
        /// that queue installs (see <c>InstallService.Add</c>) should pass the install
        /// directories of any not-yet-completed queue items here.
        /// </param>
        public async Task<string> GenerateInstallDirectoryAsync(
            Guid gameId,
            string baseDirectory,
            string? version = null,
            IEnumerable<string>? reservedDirectories = null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("baseDirectory is required.", nameof(baseDirectory));

            var reserved = new HashSet<string>(
                (reservedDirectories ?? Enumerable.Empty<string>())
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Select(NormalizeDirectory));

            bool IsReserved(string candidate) => reserved.Contains(NormalizeDirectory(candidate));

            var hasExisting = await HasInstallationsAsync(gameId);

            if (!hasExisting && !await IsInstallDirectoryInUseAsync(baseDirectory) && !IsReserved(baseDirectory))
                return baseDirectory;

            var trimmed = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(trimmed) ?? string.Empty;
            var name = Path.GetFileName(trimmed);

            if (string.IsNullOrEmpty(name))
                name = trimmed;

            var suffix = string.IsNullOrWhiteSpace(version)
                ? "New Install"
                : version.SanitizeFilename();

            var candidate = CombineSibling(parent, $"{name} ({suffix})");
            var attempt = 1;

            while (await IsInstallDirectoryInUseAsync(candidate) || IsReserved(candidate))
            {
                attempt++;
                candidate = CombineSibling(parent, $"{name} ({suffix}) ({attempt})");
            }

            return candidate;
        }

        // ── Per-installation add-on tracking ────────────────────────────────────────────────
        // Add-ons install into a specific base installation's directory, so their install state
        // must be scoped to that installation (GameInstallationAddon) rather than tracked once
        // globally per addon Game row. The addon's own Game.Installed/InstallDirectory fields are
        // still kept in sync — but only mirroring the *selected* installation — by
        // SyncLegacyMirrorsAsync below, for transitional callers that read them directly.

        public async Task<List<GameInstallationAddon>> GetInstalledAddonsForInstallationAsync(Guid installationId)
        {
            return await Context.Set<GameInstallationAddon>()
                .Where(a => a.GameInstallationId == installationId && a.Installed)
                .ToListAsync();
        }

        public async Task<bool> IsAddonInstalledForInstallationAsync(Guid installationId, Guid addonGameId)
        {
            return await Context.Set<GameInstallationAddon>()
                .AnyAsync(a => a.GameInstallationId == installationId && a.AddonGameId == addonGameId && a.Installed);
        }

        public async Task SetAddonInstalledAsync(Guid installationId, Guid addonGameId, string? version, Guid? archiveId = null)
        {
            var addon = await Context.Set<GameInstallationAddon>()
                .FirstOrDefaultAsync(a => a.GameInstallationId == installationId && a.AddonGameId == addonGameId);

            if (addon == null)
            {
                addon = new GameInstallationAddon { GameInstallationId = installationId, AddonGameId = addonGameId };
                await Context.Set<GameInstallationAddon>().AddAsync(addon);
            }

            addon.Installed = true;
            addon.InstalledVersion = version;
            addon.ArchiveId = archiveId ?? addon.ArchiveId;
            addon.InstalledOn ??= DateTime.Now;

            await Context.SaveChangesAsync();
        }

        /// <summary>
        /// Clears the install state for an addon on a specific installation without removing the
        /// installation/addon association, mirroring ToolService.SetToolUninstalledAsync's shape.
        /// </summary>
        public async Task SetAddonUninstalledAsync(Guid installationId, Guid addonGameId)
        {
            var addon = await Context.Set<GameInstallationAddon>()
                .FirstOrDefaultAsync(a => a.GameInstallationId == installationId && a.AddonGameId == addonGameId);

            if (addon == null)
                return;

            addon.Installed = false;
            addon.InstalledVersion = null;
            addon.InstalledOn = null;

            await Context.SaveChangesAsync();
        }

        /// <summary>
        /// The add-on game ids that have a <see cref="GameInstallationAddon"/> tracking row against
        /// the given installation, whether or not that row currently says installed. Deleting an
        /// installation cascades its tracking rows away, so callers that need to reason about what
        /// an installation was responsible for must capture this <em>before</em> the delete.
        /// </summary>
        public async Task<List<Guid>> GetTrackedAddonIdsForInstallationAsync(Guid installationId)
        {
            return await Context.Set<GameInstallationAddon>()
                .Where(a => a.GameInstallationId == installationId)
                .Select(a => a.AddonGameId)
                .Distinct()
                .ToListAsync();
        }

        /// <summary>
        /// Clears the legacy Game.Installed/InstallDirectory/InstalledVersion/InstalledOn mirror
        /// for each of <paramref name="addonGameIds"/> that no longer has a
        /// <see cref="GameInstallationAddon"/> row against ANY surviving installation of
        /// <paramref name="gameId"/>.
        ///
        /// This exists because deleting an installation cascades its add-on tracking rows away.
        /// <see cref="SyncLegacyMirrorsAsync"/> deliberately refuses to clear an add-on's legacy
        /// mirror when no tracking row exists for it anywhere (it cannot tell genuinely unmigrated
        /// legacy state from a real "not installed" fact), so after a cascade the add-on would be
        /// left permanently reporting installed even though its files were just removed with the
        /// installation. Callers pass exactly the add-ons the deleted installation was tracking,
        /// captured beforehand via <see cref="GetTrackedAddonIdsForInstallationAsync"/>, so:
        /// unrelated add-ons carrying genuinely unmigrated legacy state are never touched, and an
        /// add-on that is also installed on a surviving sibling installation keeps its state.
        /// </summary>
        public async Task ClearOrphanedAddonLegacyStateAsync(Guid gameId, IEnumerable<Guid>? addonGameIds)
        {
            var candidates = (addonGameIds ?? Enumerable.Empty<Guid>()).Distinct().ToList();

            if (candidates.Count == 0)
                return;

            var stillTracked = new HashSet<Guid>(
                await (from a in Context.Set<GameInstallationAddon>()
                       join i in Context.Set<GameInstallation>() on a.GameInstallationId equals i.Id
                       where i.GameId == gameId && candidates.Contains(a.AddonGameId)
                       select a.AddonGameId)
                    .Distinct()
                    .ToListAsync());

            var orphaned = candidates.Where(id => !stillTracked.Contains(id)).ToList();

            if (orphaned.Count == 0)
                return;

            var addons = await Context.Set<Game>()
                .Where(g => orphaned.Contains(g.Id))
                .ToListAsync();

            foreach (var addon in addons)
            {
                addon.Installed = false;
                addon.InstallDirectory = null;
                addon.InstalledVersion = null;
                addon.InstalledOn = null;
            }

            if (addons.Count > 0)
                await Context.SaveChangesAsync();
        }

        // ── Legacy field mirroring ───────────────────────────────────────────────────────────

        /// <summary>
        /// Mirrors the currently selected installation (if any) onto the legacy single-install
        /// <see cref="Game"/> fields (Installed/InstallDirectory/InstalledVersion/InstalledOn),
        /// mirrors that installation's <see cref="GameInstallationAddon"/> rows onto each addon's
        /// own legacy Game fields, and mirrors its <see cref="GameInstallationTool"/> rows onto the
        /// legacy per-game <see cref="GameTool"/> rows. Every transitional caller that still reads
        /// Game/GameTool directly (the action bar, CLI, tool list, etc.) always reflects the
        /// SELECTED installation without needing to be rewritten to installation instances in this
        /// phase. Call this after any operation that installs, uninstalls, updates, moves, or
        /// changes the selected installation of a game.
        /// </summary>
        public async Task SyncLegacyMirrorsAsync(Guid gameId)
        {
            var game = await Context.Set<Game>().FindAsync(gameId);

            if (game == null)
                return;

            var selected = await GetSelectedInstallationAsync(gameId);

            game.Installed = selected != null;
            game.InstallDirectory = selected?.InstallDirectory;
            game.InstalledVersion = selected?.Version;
            game.InstalledOn = selected?.InstalledOn;

            await Context.SaveChangesAsync();

            await SyncAddonMirrorsAsync(gameId, selected);
            await SyncToolMirrorsAsync(gameId, selected);
        }

        private async Task SyncAddonMirrorsAsync(Guid gameId, GameInstallation? selected)
        {
            var addons = await Context.Set<Game>().Where(g => g.BaseGameId == gameId).ToListAsync();

            if (addons.Count == 0)
                return;

            var installedAddons = selected == null
                ? new List<GameInstallationAddon>()
                : await Context.Set<GameInstallationAddon>()
                    .Where(a => a.GameInstallationId == selected.Id)
                    .ToListAsync();

            var byAddonId = installedAddons.ToDictionary(a => a.AddonGameId);

            // Add-ons for which per-installation tracking has been explicitly established
            // *somewhere* for this game (any installation, not just the selected one) — used
            // below to distinguish a genuine "not installed on this installation" fact from
            // legacy/unmigrated data that simply has no GameInstallationAddon row yet for any
            // installation (e.g. a pre-migration install whose add-on backfill hasn't run).
            var addonIdsWithAnyTracking = new HashSet<Guid>(
                await (from a in Context.Set<GameInstallationAddon>()
                       join i in Context.Set<GameInstallation>() on a.GameInstallationId equals i.Id
                       where i.GameId == gameId
                       select a.AddonGameId)
                    .Distinct()
                    .ToListAsync());

            foreach (var addon in addons)
            {
                if (byAddonId.TryGetValue(addon.Id, out var installedAddon) && installedAddon.Installed)
                {
                    addon.Installed = true;
                    addon.InstallDirectory = selected!.InstallDirectory;
                    addon.InstalledVersion = installedAddon.InstalledVersion;
                    addon.InstalledOn = installedAddon.InstalledOn;
                }
                else if (addonIdsWithAnyTracking.Contains(addon.Id) || !addon.Installed)
                {
                    // Either this add-on has an explicit association recorded elsewhere for this
                    // game (so its absence on the selected installation is authoritative — e.g.
                    // switching to a sibling installation that legitimately has no add-ons), or
                    // the legacy mirror is already showing not-installed (nothing to protect).
                    addon.Installed = false;
                    addon.InstallDirectory = null;
                    addon.InstalledVersion = null;
                    addon.InstalledOn = null;
                }
                // else: addon.Installed is legacy-true and no GameInstallationAddon row has ever
                // been created for it under any installation of this game — leave the legacy
                // mirror untouched rather than silently clearing real installed state that has no
                // explicit replacement/uninstall intent behind it.
            }

            await Context.SaveChangesAsync();
        }

        private async Task SyncToolMirrorsAsync(Guid gameId, GameInstallation? selected)
        {
            var gameTools = await Context.Set<GameTool>().Where(gt => gt.GameId == gameId).ToListAsync();

            var installationTools = selected == null
                ? new List<GameInstallationTool>()
                : await Context.Set<GameInstallationTool>().Where(t => t.GameInstallationId == selected.Id).ToListAsync();

            var byToolId = installationTools.ToDictionary(t => t.ToolId);

            foreach (var gameTool in gameTools)
            {
                if (byToolId.TryGetValue(gameTool.ToolId, out var installationTool))
                {
                    gameTool.Installed = installationTool.Installed;
                    gameTool.InstallDirectory = installationTool.InstallDirectory;
                    gameTool.InstalledVersion = installationTool.InstalledVersion;
                    gameTool.InstalledOn = installationTool.InstalledOn;
                }
                else
                {
                    gameTool.Installed = false;
                    gameTool.InstallDirectory = null;
                    gameTool.InstalledVersion = null;
                    gameTool.InstalledOn = null;
                }
            }

            // Any installation-tool row not yet mirrored (a tool installed for this installation
            // that has no legacy GameTool row at all yet) needs one created so legacy per-game
            // tool-state readers see it too.
            var existingToolIds = gameTools.Select(gt => gt.ToolId).ToHashSet();

            foreach (var installationTool in installationTools.Where(t => !existingToolIds.Contains(t.ToolId)))
            {
                await Context.Set<GameTool>().AddAsync(new GameTool
                {
                    GameId = gameId,
                    ToolId = installationTool.ToolId,
                    Installed = installationTool.Installed,
                    InstallDirectory = installationTool.InstallDirectory,
                    InstalledVersion = installationTool.InstalledVersion,
                    InstalledOn = installationTool.InstalledOn,
                });
            }

            await Context.SaveChangesAsync();
        }

        /// <summary>
        /// Starts a transaction when the underlying provider supports it. EF Core's InMemory
        /// provider (used by unit tests) doesn't support relational transactions, so callers must
        /// tolerate a null transaction and treat each SaveChangesAsync as immediately committed.
        /// </summary>
        private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        {
            if (!Context.Database.IsRelational())
                return null;

            return await Context.Database.BeginTransactionAsync();
        }

        private static string CombineSibling(string parent, string name) =>
            string.IsNullOrEmpty(parent) ? name : Path.Combine(parent, name);

        private static string NormalizeDirectory(string path) => path.Trim().ToUpperInvariant();

        private async Task ClearSelectionAsync(Guid gameId)
        {
            var selected = await Context.Set<GameInstallation>()
                .Where(i => i.GameId == gameId && i.IsSelected)
                .ToListAsync();

            foreach (var installation in selected)
                installation.IsSelected = false;

            if (selected.Count > 0)
                await Context.SaveChangesAsync();
        }

        private async Task PointGameAtSelectedInstallationAsync(Guid gameId, Guid? installationId)
        {
            var game = await Context.Set<Game>().FindAsync(gameId);

            if (game == null)
                return;

            game.SelectedInstallationId = installationId;
            await Context.SaveChangesAsync();
        }
    }
}
