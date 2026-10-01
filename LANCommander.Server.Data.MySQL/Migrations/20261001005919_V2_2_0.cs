using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class V2_2_0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Changelog",
                table: "Archive");

            migrationBuilder.AddColumn<Guid>(
                name: "GameVersionId",
                table: "Scripts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "GameVersionId",
                table: "SavePaths",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "InstallTo",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OptionSchema",
                table: "Games",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "Games",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInLibrary",
                table: "Games",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GameVersionId",
                table: "Archive",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "GameVersionId",
                table: "Actions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "GameVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Version = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Changelog = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Published = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GameId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OptionSchema = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedOn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedOn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameVersions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameVersions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GameVersions_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GameVersionRedistributables",
                columns: table => new
                {
                    GameVersionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RedistributableId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Options = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameVersionRedistributables", x => new { x.GameVersionId, x.RedistributableId });
                    table.ForeignKey(
                        name: "FK_GameVersionRedistributables_GameVersions_GameVersionId",
                        column: x => x.GameVersionId,
                        principalTable: "GameVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameVersionRedistributables_Redistributables_Redistributable~",
                        column: x => x.RedistributableId,
                        principalTable: "Redistributables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Scripts_GameVersionId",
                table: "Scripts",
                column: "GameVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavePaths_GameVersionId",
                table: "SavePaths",
                column: "GameVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Archive_GameVersionId",
                table: "Archive",
                column: "GameVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Actions_GameVersionId",
                table: "Actions",
                column: "GameVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameVersionRedistributables_RedistributableId",
                table: "GameVersionRedistributables",
                column: "RedistributableId");

            migrationBuilder.CreateIndex(
                name: "IX_GameVersions_CreatedById",
                table: "GameVersions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GameVersions_GameId",
                table: "GameVersions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameVersions_UpdatedById",
                table: "GameVersions",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_GameVersions_GameVersionId",
                table: "Actions",
                column: "GameVersionId",
                principalTable: "GameVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_GameVersions_GameVersionId",
                table: "Archive",
                column: "GameVersionId",
                principalTable: "GameVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_GameVersions_GameVersionId",
                table: "SavePaths",
                column: "GameVersionId",
                principalTable: "GameVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_GameVersions_GameVersionId",
                table: "Scripts",
                column: "GameVersionId",
                principalTable: "GameVersions",
                principalColumn: "Id");

            // Derive the new fields from the legacy types (GameTypeHelper.FromLegacyType):
            // Types: 0 MainGame, 1 Expansion, 2 StandaloneExpansion, 3 Mod, 4 StandaloneMod
            // InstallTo: 0 OwnDirectory, 1 BaseGameDirectory
            migrationBuilder.Sql("UPDATE `Games` SET `ShowInLibrary` = 0 WHERE `Type` IN (1, 3)");
            migrationBuilder.Sql("UPDATE `Games` SET `InstallTo` = 1 WHERE `Type` IN (1, 3, 4) AND `BaseGameId` IS NOT NULL");
            migrationBuilder.Sql("UPDATE `Games` SET `Type` = 1 WHERE `Type` = 2");
            migrationBuilder.Sql("UPDATE `Games` SET `Type` = 3 WHERE `Type` = 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `Games` SET `Type` = 2 WHERE `Type` = 1 AND `ShowInLibrary` = 1");
            migrationBuilder.Sql("UPDATE `Games` SET `Type` = 4 WHERE `Type` = 3 AND `ShowInLibrary` = 1");

            migrationBuilder.DropForeignKey(
                name: "FK_Actions_GameVersions_GameVersionId",
                table: "Actions");

            migrationBuilder.DropForeignKey(
                name: "FK_Archive_GameVersions_GameVersionId",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_GameVersions_GameVersionId",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_GameVersions_GameVersionId",
                table: "Scripts");

            migrationBuilder.DropTable(
                name: "GameVersionRedistributables");

            migrationBuilder.DropTable(
                name: "GameVersions");

            migrationBuilder.DropIndex(
                name: "IX_Scripts_GameVersionId",
                table: "Scripts");

            migrationBuilder.DropIndex(
                name: "IX_SavePaths_GameVersionId",
                table: "SavePaths");

            migrationBuilder.DropIndex(
                name: "IX_Archive_GameVersionId",
                table: "Archive");

            migrationBuilder.DropIndex(
                name: "IX_Actions_GameVersionId",
                table: "Actions");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "Scripts");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "SavePaths");

            migrationBuilder.DropColumn(
                name: "InstallTo",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "OptionSchema",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ShowInLibrary",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "Archive");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "Actions");

            migrationBuilder.AddColumn<string>(
                name: "Changelog",
                table: "Archive",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
