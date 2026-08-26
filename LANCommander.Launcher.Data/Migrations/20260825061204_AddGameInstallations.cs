using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Launcher.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameInstallations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SelectedInstallationId",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArchiveId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<string>(type: "TEXT", nullable: true),
                    InstallDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    InstalledOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DisplayLabel = table.Column<string>(type: "TEXT", nullable: true),
                    IsSelected = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameInstallations_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameInstallationAddons",
                columns: table => new
                {
                    GameInstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddonGameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArchiveId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Installed = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstalledVersion = table.Column<string>(type: "TEXT", nullable: true),
                    InstalledOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameInstallationAddons", x => new { x.GameInstallationId, x.AddonGameId });
                    table.ForeignKey(
                        name: "FK_GameInstallationAddons_GameInstallations_GameInstallationId",
                        column: x => x.GameInstallationId,
                        principalTable: "GameInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameInstallationAddons_Games_AddonGameId",
                        column: x => x.AddonGameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameInstallationTools",
                columns: table => new
                {
                    GameInstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Installed = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstallDirectory = table.Column<string>(type: "TEXT", nullable: true),
                    InstalledVersion = table.Column<string>(type: "TEXT", nullable: true),
                    InstalledOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameInstallationTools", x => new { x.GameInstallationId, x.ToolId });
                    table.ForeignKey(
                        name: "FK_GameInstallationTools_GameInstallations_GameInstallationId",
                        column: x => x.GameInstallationId,
                        principalTable: "GameInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameInstallationTools_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Preserve existing installs: every game that was reporting Installed=1 with a
            // non-empty InstallDirectory becomes one selected GameInstallation carrying its
            // legacy install metadata. Games that were never installed (or have an empty
            // directory) get no installation rows.
            //
            // Expansion/Mod/StandaloneMod rows (GameType 1/3/4 - see LANCommander.SDK.Enums.GameType)
            // with a BaseGameId are deliberately excluded here: they are overlays that share their
            // base game's install directory (see GameClient.GetInstallDirectory), and the old
            // single-install model mirrored that same shared directory onto their own legacy
            // Installed/InstallDirectory fields too (see GameInstallationService.SyncAddonMirrorsAsync).
            // Including them here would attempt to INSERT a second GameInstallation row at the
            // exact same InstallDirectory as their base game, violating the InstallDirectory
            // uniqueness invariant (IX_GameInstallations_InstallDirectory) and failing the whole
            // migration for any database with an installed add-on alongside its base game. Their
            // installed state is backfilled onto GameInstallationAddons instead, below.
            //
            // The legacy schema had no uniqueness constraint on Games.InstallDirectory at all, so
            // two non-overlay games can legitimately share a path in a real database — two titles
            // that sanitize to the same folder name, a re-imported/duplicated library entry, or
            // simply historical corruption. IX_GameInstallations_InstallDirectory (created below)
            // is globally unique, so backfilling all of them would abort the migration on startup
            // and leave the launcher unable to open at all. Instead exactly one row per distinct
            // directory is claimed, chosen deterministically as the lowest Game Id so the outcome
            // is stable and repeatable rather than dependent on scan order. The games that do not
            // win the directory keep their legacy Installed/InstallDirectory/InstalledVersion
            // fields exactly as they were, so they still read as installed through the legacy
            // fallback paths (Game.CurrentInstallation, the action bar's legacy update flow) —
            // dropping their state would be a silent data loss, refusing to migrate would be worse.
            migrationBuilder.Sql(@"
                INSERT INTO GameInstallations (Id, GameId, ArchiveId, Version, InstallDirectory, InstalledOn, DisplayLabel, IsSelected, ImportedOn, CreatedOn, UpdatedOn)
                SELECT
                    -- Microsoft.Data.Sqlite/EF Core store Guid values as UPPERCASE hex text (unlike
                    -- the lower() pattern used elsewhere for opaque, never-FK-compared ids); this
                    -- generated id is later compared against EF-supplied Guid parameters via FK
                    -- joins (GameInstallationTools.GameInstallationId, Games.SelectedInstallationId),
                    -- so it must match EF's casing or those equality lookups silently return no rows.
                    upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89AB', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6))),
                    g.Id,
                    NULL,
                    g.InstalledVersion,
                    g.InstallDirectory,
                    g.InstalledOn,
                    NULL,
                    1,
                    g.ImportedOn,
                    COALESCE(g.InstalledOn, g.CreatedOn),
                    g.UpdatedOn
                FROM Games g
                WHERE g.Installed = 1 AND g.InstallDirectory IS NOT NULL AND g.InstallDirectory <> ''
                    AND NOT (g.Type IN (1, 3, 4) AND g.BaseGameId IS NOT NULL)
                    AND g.Id = (
                        SELECT MIN(dup.Id)
                        FROM Games dup
                        WHERE dup.Installed = 1
                            AND dup.InstallDirectory = g.InstallDirectory
                            AND NOT (dup.Type IN (1, 3, 4) AND dup.BaseGameId IS NOT NULL)
                    );
            ");

            // Point each migrated game at its newly created selected installation so
            // Game.SelectedInstallation/CurrentInstallation resolve immediately without requiring
            // a re-import. Scoped by existence of the row rather than by repeating the backfill's
            // filters, so a game that lost a duplicate-directory tie above is left with a NULL
            // SelectedInstallationId instead of being pointed at another game's installation.
            migrationBuilder.Sql(@"
                UPDATE Games
                SET SelectedInstallationId = (
                    SELECT gi.Id FROM GameInstallations gi WHERE gi.GameId = Games.Id AND gi.IsSelected = 1
                )
                WHERE EXISTS (
                    SELECT 1 FROM GameInstallations gi WHERE gi.GameId = Games.Id AND gi.IsSelected = 1
                );
            ");

            // Backfill GameInstallationAddons for installed legacy Expansion/Mod/StandaloneMod
            // rows (excluded above) onto their base game's newly migrated selected installation,
            // preserving the version/install date that was recorded under the old single-install
            // model. These overlay rows never get their own GameInstallation — see the exclusion
            // above — so without this backfill an already-installed add-on would silently show as
            // not installed after migrating (SyncAddonMirrorsAsync only trusts GameInstallationAddons).
            //
            // The inner join is what keeps this attached strictly to rows the backfill above
            // actually inserted: an add-on whose base game got no installation row (it lost a
            // duplicate-directory tie) contributes nothing rather than latching onto some other
            // game's installation. Games.Id is unique, so one add-on can never produce two rows for
            // the same installation and the (GameInstallationId, AddonGameId) primary key holds.
            migrationBuilder.Sql(@"
                INSERT INTO GameInstallationAddons (GameInstallationId, AddonGameId, ArchiveId, Installed, InstalledVersion, InstalledOn)
                SELECT gi.Id, g.Id, NULL, 1, g.InstalledVersion, g.InstalledOn
                FROM Games g
                JOIN GameInstallations gi ON gi.GameId = g.BaseGameId AND gi.IsSelected = 1
                WHERE g.Installed = 1 AND g.BaseGameId IS NOT NULL AND g.Type IN (1, 3, 4);
            ");

            // Migrate existing per-game tool install state (GameTool, kept for compatibility) onto
            // the new installation instance so per-installation tool tracking starts consistent
            // with what was already recorded as installed.
            //
            // Same invariant as the add-on backfill: the inner join attaches tool state only to
            // installations that were actually inserted, so a game that lost a duplicate-directory
            // tie keeps its tool state in the legacy GameTool table instead of writing it onto a
            // different game's installation. GameTool is keyed on (GameId, ToolId) and each game
            // has at most one installation row, so (GameInstallationId, ToolId) stays unique.
            migrationBuilder.Sql(@"
                INSERT INTO GameInstallationTools (GameInstallationId, ToolId, Installed, InstallDirectory, InstalledVersion, InstalledOn)
                SELECT gi.Id, gt.ToolId, gt.Installed, gt.InstallDirectory, gt.InstalledVersion, gt.InstalledOn
                FROM GameTool gt
                JOIN GameInstallations gi ON gi.GameId = gt.GameId AND gi.IsSelected = 1
                WHERE gt.Installed = 1;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Games_SelectedInstallationId",
                table: "Games",
                column: "SelectedInstallationId");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstallationAddons_AddonGameId",
                table: "GameInstallationAddons",
                column: "AddonGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstallations_GameId_Selected",
                table: "GameInstallations",
                column: "GameId",
                unique: true,
                filter: "\"IsSelected\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstallations_InstallDirectory",
                table: "GameInstallations",
                column: "InstallDirectory",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameInstallationTools_ToolId",
                table: "GameInstallationTools",
                column: "ToolId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_GameInstallations_SelectedInstallationId",
                table: "Games",
                column: "SelectedInstallationId",
                principalTable: "GameInstallations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_GameInstallations_SelectedInstallationId",
                table: "Games");

            migrationBuilder.DropTable(
                name: "GameInstallationAddons");

            migrationBuilder.DropTable(
                name: "GameInstallationTools");

            migrationBuilder.DropTable(
                name: "GameInstallations");

            migrationBuilder.DropIndex(
                name: "IX_Games_SelectedInstallationId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "SelectedInstallationId",
                table: "Games");
        }
    }
}
