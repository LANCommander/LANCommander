using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class VersionRedistributablesAndOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OptionSchema",
                table: "GameVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameVersionRedistributables",
                columns: table => new
                {
                    GameVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RedistributableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Options = table.Column<string>(type: "TEXT", nullable: true)
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
                        name: "FK_GameVersionRedistributables_Redistributables_RedistributableId",
                        column: x => x.RedistributableId,
                        principalTable: "Redistributables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameVersionRedistributables_RedistributableId",
                table: "GameVersionRedistributables",
                column: "RedistributableId");

            // Until now every version shared the game's option schema and redistributables, so each
            // existing version starts with them
            migrationBuilder.Sql(@"UPDATE GameVersions SET OptionSchema = (SELECT g.OptionSchema FROM Games g WHERE g.Id = GameVersions.GameId);");

            migrationBuilder.Sql(@"INSERT INTO GameVersionRedistributables (GameVersionId, RedistributableId, Options)
SELECT v.Id, gr.RedistributableId, gr.Options FROM GameVersions v JOIN GameRedistributable gr ON gr.GameId = v.GameId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameVersionRedistributables");

            migrationBuilder.DropColumn(
                name: "OptionSchema",
                table: "GameVersions");
        }
    }
}
