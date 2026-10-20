using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddGameInstallLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstallTo",
                table: "Games",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInLibrary",
                table: "Games",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Derive the new fields from the legacy types (GameTypeHelper.FromLegacyType):
            // Types: 0 MainGame, 1 Expansion, 2 StandaloneExpansion, 3 Mod, 4 StandaloneMod
            // InstallTo: 0 OwnDirectory, 1 BaseGameDirectory
            migrationBuilder.Sql(@"UPDATE ""Games"" SET ""ShowInLibrary"" = false WHERE ""Type"" IN (1, 3)");
            migrationBuilder.Sql(@"UPDATE ""Games"" SET ""InstallTo"" = 1 WHERE ""Type"" IN (1, 3, 4) AND ""BaseGameId"" IS NOT NULL");
            migrationBuilder.Sql(@"UPDATE ""Games"" SET ""Type"" = 1 WHERE ""Type"" = 2");
            migrationBuilder.Sql(@"UPDATE ""Games"" SET ""Type"" = 3 WHERE ""Type"" = 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Games"" SET ""Type"" = 2 WHERE ""Type"" = 1 AND ""ShowInLibrary"" = true");
            migrationBuilder.Sql(@"UPDATE ""Games"" SET ""Type"" = 4 WHERE ""Type"" = 3 AND ""ShowInLibrary"" = true");

            migrationBuilder.DropColumn(
                name: "InstallTo",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ShowInLibrary",
                table: "Games");
        }
    }
}
