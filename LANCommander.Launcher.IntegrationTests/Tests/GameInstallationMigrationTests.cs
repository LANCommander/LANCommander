using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.IntegrationTests.Tests;

/// <summary>
/// Verifies the AddGameInstallations migration preserves existing single installs. Applies real
/// SQLite migrations (not EF InMemory, which never runs migrations) against a throwaway file
/// database: seeds legacy-shape data at the prior migration, applies the new migration, then
/// asserts the resulting GameInstallation/GameInstallationTool rows and Games.SelectedInstallationId.
/// </summary>
public class GameInstallationMigrationTests : IAsyncLifetime
{
    private const string PriorMigration = "20260627164452_AddPerGameToolInstallState";
    private const string TargetMigration = "20260825061204_AddGameInstallations";

    private readonly string _dbPath = Path.Combine(
        AppContext.BaseDirectory,
        $"migration-test-{Guid.NewGuid():N}.db");

    private DatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;

        return new DatabaseContext(NullLoggerFactory.Instance, options);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        // Pooling=False (above) means each connection's native handle is closed as soon as the
        // DbConnection/DbContext is disposed, so the file is free to delete here. Without it,
        // Microsoft.Data.Sqlite's connection pool can keep a handle open past disposal and
        // deleting the file races with the pool, throwing IOException on Windows.
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Migration_creates_selected_installation_for_existing_installed_game()
    {
        var gameId = Guid.NewGuid();
        var toolId = Guid.NewGuid();
        var installedOn = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            // Seed rows in the legacy (pre-installation-instance) shape directly, since the
            // current C# model no longer matches the schema as it existed at PriorMigration.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Half-Life', 1, 'C:\\Games\\HalfLife', '1.0.0', {1}, 0, 0, {1}, {1}, {1})",
                gameId, installedOn);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Tools (Id, Name, ImportedOn, CreatedOn, UpdatedOn) VALUES ({0}, 'Redist', {1}, {1}, {1})",
                toolId, installedOn);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO GameTool (GameId, ToolId, Installed, InstallDirectory, InstalledVersion, InstalledOn) " +
                "VALUES ({0}, {1}, 1, 'C:\\Games\\HalfLife', '2.0.0', {2})",
                gameId, toolId, installedOn);

            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        var installation = await readContext.Set<GameInstallation>().SingleAsync(i => i.GameId == gameId);
        installation.IsSelected.ShouldBeTrue();
        installation.InstallDirectory.ShouldBe("C:\\Games\\HalfLife");
        installation.Version.ShouldBe("1.0.0");
        installation.InstalledOn.ShouldBe(installedOn);
        installation.ArchiveId.ShouldBeNull();

        var game = await readContext.Games!.SingleAsync(g => g.Id == gameId);
        game.SelectedInstallationId.ShouldBe(installation.Id);

        var installationTool = await readContext.Set<GameInstallationTool>()
            .SingleAsync(t => t.GameInstallationId == installation.Id);
        installationTool.ToolId.ShouldBe(toolId);
        installationTool.Installed.ShouldBeTrue();
        installationTool.InstalledVersion.ShouldBe("2.0.0");
    }

    [Fact]
    public async Task Migration_creates_no_installation_for_uninstalled_game()
    {
        var gameId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Not Installed', 0, 0, 0, {1}, {1}, {1})",
                gameId, now);

            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        (await readContext.Set<GameInstallation>().AnyAsync(i => i.GameId == gameId)).ShouldBeFalse();

        var game = await readContext.Games!.SingleAsync(g => g.Id == gameId);
        game.SelectedInstallationId.ShouldBeNull();
    }

    [Fact]
    public async Task Migration_ignores_installed_game_with_empty_install_directory()
    {
        var gameId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            // Data-quality edge case: Installed=1 but no directory recorded. Must not produce a
            // GameInstallation row with an empty (and therefore collision-prone) directory.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Weird', 1, '', 0, 0, {1}, {1}, {1})",
                gameId, now);

            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        (await readContext.Set<GameInstallation>().AnyAsync(i => i.GameId == gameId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Migration_does_not_duplicate_install_directory_for_installed_addons_sharing_base_path()
    {
        var baseGameId = Guid.NewGuid();
        var expansionId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var installedOn = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var modInstalledOn = installedOn.AddDays(1);
        const string sharedDirectory = "C:\\Games\\HalfLife";

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            // Base game (GameType.MainGame = 0), installed normally.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Half-Life', 1, {2}, '1.0.0', {1}, 0, 0, {1}, {1}, {1})",
                baseGameId, installedOn, sharedDirectory);

            // Installed Expansion (GameType.Expansion = 1) sharing the base game's directory —
            // the old single-install model mirrored the same InstallDirectory onto the addon's
            // own legacy fields too (see GameInstallationService.SyncAddonMirrorsAsync).
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, BaseGameId, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Opposing Force', 1, {2}, '1.0.0', {1}, 1, {3}, 0, {1}, {1}, {1})",
                expansionId, installedOn, sharedDirectory, baseGameId);

            // Installed Mod (GameType.Mod = 3) also sharing the same directory, with its own
            // distinct installed version/date.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, BaseGameId, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Deathmatch Classic', 1, {2}, '2.5.0', {1}, 3, {3}, 0, {1}, {1}, {1})",
                modId, modInstalledOn, sharedDirectory, baseGameId);

            // Must not throw. This is the CRITICAL bug: without the base/BaseGameId exclusion,
            // the migration would attempt to INSERT a GameInstallation row for the expansion and
            // the mod at the exact same InstallDirectory as the base game, violating
            // IX_GameInstallations_InstallDirectory's uniqueness invariant and failing outright
            // for any database with an installed add-on alongside its base game.
            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        // Exactly one GameInstallation exists at the shared directory — for the base game only.
        var installationsAtSharedDirectory = await readContext.Set<GameInstallation>()
            .Where(i => i.InstallDirectory == sharedDirectory)
            .ToListAsync();
        installationsAtSharedDirectory.Count.ShouldBe(1);

        var installation = installationsAtSharedDirectory.Single();
        installation.GameId.ShouldBe(baseGameId);
        installation.IsSelected.ShouldBeTrue();

        // The addons must never get their own GameInstallation row.
        (await readContext.Set<GameInstallation>().AnyAsync(i => i.GameId == expansionId)).ShouldBeFalse();
        (await readContext.Set<GameInstallation>().AnyAsync(i => i.GameId == modId)).ShouldBeFalse();

        // Each addon's installed state is backfilled onto GameInstallationAddons instead, scoped
        // to the base game's installation, preserving version/date.
        var addonRows = await readContext.Set<GameInstallationAddon>()
            .Where(a => a.GameInstallationId == installation.Id)
            .ToListAsync();
        addonRows.Count.ShouldBe(2);

        var expansionRow = addonRows.Single(a => a.AddonGameId == expansionId);
        expansionRow.Installed.ShouldBeTrue();
        expansionRow.InstalledVersion.ShouldBe("1.0.0");
        expansionRow.InstalledOn.ShouldBe(installedOn);

        var modRow = addonRows.Single(a => a.AddonGameId == modId);
        modRow.Installed.ShouldBeTrue();
        modRow.InstalledVersion.ShouldBe("2.5.0");
        modRow.InstalledOn.ShouldBe(modInstalledOn);
    }

    [Fact]
    public async Task Migration_excludes_installed_StandaloneMod_from_its_own_installation_row()
    {
        var baseGameId = Guid.NewGuid();
        var standaloneModId = Guid.NewGuid();
        var now = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        const string sharedDirectory = "C:\\Games\\Quake";

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Quake', 1, {2}, '1.0.0', {1}, 0, 0, {1}, {1}, {1})",
                baseGameId, now, sharedDirectory);

            // GameType.StandaloneMod = 4 — presented as its own library entry, but per
            // GameClient.GetInstallDirectory it is still an overlay that shares its base game's
            // directory, so it must be treated the same as Expansion/Mod here.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, BaseGameId, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Quake Total Conversion', 1, {2}, '1.0.0', {1}, 4, {3}, 0, {1}, {1}, {1})",
                standaloneModId, now, sharedDirectory, baseGameId);

            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        (await readContext.Set<GameInstallation>().Where(i => i.InstallDirectory == sharedDirectory).CountAsync()).ShouldBe(1);
        (await readContext.Set<GameInstallation>().AnyAsync(i => i.GameId == standaloneModId)).ShouldBeFalse();

        var installation = await readContext.Set<GameInstallation>().SingleAsync(i => i.GameId == baseGameId);
        var addonRow = await readContext.Set<GameInstallationAddon>().SingleAsync(a => a.GameInstallationId == installation.Id);
        addonRow.AddonGameId.ShouldBe(standaloneModId);
        addonRow.Installed.ShouldBeTrue();
    }

    [Fact]
    public async Task Migration_still_creates_its_own_installation_for_installed_StandaloneExpansion()
    {
        var gameId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            // GameType.StandaloneExpansion = 2 — unlike Expansion/Mod/StandaloneMod, this type is
            // NOT an overlay (see GameClient.GetInstallDirectory) and gets its own independent
            // directory, so it must still get its own GameInstallation row.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Blue Shift', 1, 'C:\\Games\\BlueShift', '1.0.0', {1}, 2, 0, {1}, {1}, {1})",
                gameId, now);

            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        var installation = await readContext.Set<GameInstallation>().SingleAsync(i => i.GameId == gameId);
        installation.InstallDirectory.ShouldBe("C:\\Games\\BlueShift");
        installation.IsSelected.ShouldBeTrue();
    }

    // ── Duplicate InstallDirectory across non-overlay games ─────────────────────
    //
    // The legacy schema never constrained Games.InstallDirectory, so two non-overlay games can
    // genuinely share a path (titles that sanitize to the same folder name, a duplicated library
    // entry, historical corruption). IX_GameInstallations_InstallDirectory is globally unique, so
    // backfilling both would abort the migration and leave the launcher unable to start at all.

    /// <summary>
    /// Seeds two installed non-overlay games at the same directory and returns their ids ordered
    /// as SQLite compares the stored Guid text, i.e. (expected winner, expected loser).
    /// </summary>
    private static (Guid Winner, Guid Loser) OrderByStoredGuid(Guid first, Guid second)
    {
        // EF Core/Microsoft.Data.Sqlite persist Guid as uppercase 'D'-format text, and the
        // migration picks MIN(Id) over that text — a stable, repeatable total order.
        var firstText = first.ToString().ToUpperInvariant();
        var secondText = second.ToString().ToUpperInvariant();

        return string.CompareOrdinal(firstText, secondText) <= 0 ? (first, second) : (second, first);
    }

    [Fact]
    public async Task Migration_claims_shared_install_directory_once_and_keeps_legacy_state_for_the_rest()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var (winnerId, loserId) = OrderByStoredGuid(firstId, secondId);

        var winnerToolId = Guid.NewGuid();
        var loserToolId = Guid.NewGuid();
        var installedOn = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        const string sharedDirectory = "C:\\Games\\Half-Life";

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            // Two GameType.MainGame (0) rows — neither is an overlay — recorded as installed at
            // the exact same directory.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Half-Life', 1, {2}, '1.0.0', {1}, 0, 0, {1}, {1}, {1})",
                winnerId, installedOn, sharedDirectory);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Half Life', 1, {2}, '2.0.0', {1}, 0, 0, {1}, {1}, {1})",
                loserId, installedOn, sharedDirectory);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Tools (Id, Name, ImportedOn, CreatedOn, UpdatedOn) VALUES ({0}, 'Winner Tool', {1}, {1}, {1})",
                winnerToolId, installedOn);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Tools (Id, Name, ImportedOn, CreatedOn, UpdatedOn) VALUES ({0}, 'Loser Tool', {1}, {1}, {1})",
                loserToolId, installedOn);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO GameTool (GameId, ToolId, Installed, InstallDirectory, InstalledVersion, InstalledOn) VALUES ({0}, {1}, 1, {2}, '3.0.0', {3})",
                winnerId, winnerToolId, sharedDirectory, installedOn);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO GameTool (GameId, ToolId, Installed, InstallDirectory, InstalledVersion, InstalledOn) VALUES ({0}, {1}, 1, {2}, '4.0.0', {3})",
                loserId, loserToolId, sharedDirectory, installedOn);

            // Must not throw. Without deterministic de-duplication the backfill inserts two
            // GameInstallations rows at the same path and CreateIndex on the globally unique
            // IX_GameInstallations_InstallDirectory fails, taking the whole startup migration —
            // and therefore the launcher — down with it.
            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        // Exactly one installation claims the shared path, deterministically the lowest Game Id.
        var installations = await readContext.Set<GameInstallation>()
            .Where(i => i.InstallDirectory == sharedDirectory)
            .ToListAsync();
        installations.Count.ShouldBe(1);

        var installation = installations.Single();
        installation.GameId.ShouldBe(winnerId);
        installation.IsSelected.ShouldBeTrue();

        (await readContext.Set<GameInstallation>().AnyAsync(i => i.GameId == loserId)).ShouldBeFalse();

        var winner = await readContext.Games!.SingleAsync(g => g.Id == winnerId);
        winner.SelectedInstallationId.ShouldBe(installation.Id);

        // The game that did not claim the directory keeps every legacy field intact, so the
        // action bar's legacy fallback still reports it as installed instead of the migration
        // silently dropping its state. It just has no installation row (nor a dangling pointer to
        // somebody else's).
        var loser = await readContext.Games!.SingleAsync(g => g.Id == loserId);
        loser.Installed.ShouldBeTrue();
        loser.InstallDirectory.ShouldBe(sharedDirectory);
        loser.InstalledVersion.ShouldBe("2.0.0");
        loser.SelectedInstallationId.ShouldBeNull();

        // Tool state attaches only to the inserted row: the winner's tool migrates, the loser's
        // stays behind in the legacy GameTool table rather than being duplicated onto (or
        // misattributed to) the surviving installation.
        var installationTools = await readContext.Set<GameInstallationTool>().ToListAsync();
        installationTools.Count.ShouldBe(1);
        installationTools.Single().GameInstallationId.ShouldBe(installation.Id);
        installationTools.Single().ToolId.ShouldBe(winnerToolId);
        installationTools.Single().InstalledVersion.ShouldBe("3.0.0");

        // No add-ons were involved, so no add-on rows may have been invented either.
        (await readContext.Set<GameInstallationAddon>().AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Migration_with_shared_install_directory_keeps_addon_backfill_attached_to_the_inserted_row_only()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var (winnerId, loserId) = OrderByStoredGuid(firstId, secondId);

        var winnerAddonId = Guid.NewGuid();
        var loserAddonId = Guid.NewGuid();
        var installedOn = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        const string sharedDirectory = "C:\\Games\\Quake";

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Quake', 1, {2}, '1.0.0', {1}, 0, 0, {1}, {1}, {1})",
                winnerId, installedOn, sharedDirectory);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Quake ', 1, {2}, '1.0.0', {1}, 0, 0, {1}, {1}, {1})",
                loserId, installedOn, sharedDirectory);

            // One installed Expansion (GameType.Expansion = 1) overlaying each of the two games,
            // both mirroring the same shared directory the way the old single-install model did.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, BaseGameId, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Scourge of Armagon', 1, {2}, '1.1.0', {1}, 1, {3}, 0, {1}, {1}, {1})",
                winnerAddonId, installedOn, sharedDirectory, winnerId);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, BaseGameId, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Dissolution of Eternity', 1, {2}, '1.2.0', {1}, 1, {3}, 0, {1}, {1}, {1})",
                loserAddonId, installedOn, sharedDirectory, loserId);

            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        var installation = await readContext.Set<GameInstallation>().SingleAsync();
        installation.GameId.ShouldBe(winnerId);

        // Only the add-on whose base game actually produced an installation row is backfilled —
        // the other one is not silently re-parented onto the surviving installation, which would
        // both corrupt ownership and (with matching add-ons) collide on the composite key.
        var addonRows = await readContext.Set<GameInstallationAddon>().ToListAsync();
        addonRows.Count.ShouldBe(1);
        addonRows.Single().GameInstallationId.ShouldBe(installation.Id);
        addonRows.Single().AddonGameId.ShouldBe(winnerAddonId);
        addonRows.Single().InstalledVersion.ShouldBe("1.1.0");

        // The orphaned add-on keeps its legacy installed state instead of losing it.
        var loserAddon = await readContext.Games!.SingleAsync(g => g.Id == loserAddonId);
        loserAddon.Installed.ShouldBeTrue();
        loserAddon.InstallDirectory.ShouldBe(sharedDirectory);
    }

    [Fact]
    public async Task Migration_picks_the_same_winner_regardless_of_insertion_order()
    {
        // Determinism: the surviving row is chosen by Game Id, not by row/scan order, so the same
        // database migrates to the same result no matter how it was written.
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var (winnerId, loserId) = OrderByStoredGuid(firstId, secondId);
        var now = new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        const string sharedDirectory = "C:\\Games\\Doom";

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            // Deliberately insert the higher Id first.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Doom II', 1, {2}, '1.9.0', {1}, 0, 0, {1}, {1}, {1})",
                loserId, now, sharedDirectory);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Doom', 1, {2}, '1.9.0', {1}, 0, 0, {1}, {1}, {1})",
                winnerId, now, sharedDirectory);

            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        var installation = await readContext.Set<GameInstallation>().SingleAsync();
        installation.GameId.ShouldBe(winnerId);
    }

    [Fact]
    public async Task Migration_still_migrates_every_game_when_install_directories_differ()
    {
        // Contrast case: de-duplication must only ever collapse genuine collisions.
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var now = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var context = CreateContext())
        {
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PriorMigration);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Half-Life', 1, 'C:\\Games\\HalfLife', '1.0.0', {1}, 0, 0, {1}, {1}, {1})",
                firstId, now);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Games (Id, Title, Installed, InstallDirectory, InstalledVersion, InstalledOn, Type, Singleplayer, ImportedOn, CreatedOn, UpdatedOn) " +
                "VALUES ({0}, 'Quake', 1, 'C:\\Games\\Quake', '1.0.0', {1}, 0, 0, {1}, {1}, {1})",
                secondId, now);

            await migrator.MigrateAsync(TargetMigration);
        }

        await using var readContext = CreateContext();

        (await readContext.Set<GameInstallation>().CountAsync()).ShouldBe(2);

        var first = await readContext.Games!.SingleAsync(g => g.Id == firstId);
        var second = await readContext.Games!.SingleAsync(g => g.Id == secondId);

        first.SelectedInstallationId.ShouldNotBeNull();
        second.SelectedInstallationId.ShouldNotBeNull();
    }
}
