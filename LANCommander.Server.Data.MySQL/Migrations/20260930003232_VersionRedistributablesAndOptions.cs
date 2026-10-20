using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
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
                type: "longtext",
                nullable: true)
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
                name: "IX_GameVersionRedistributables_RedistributableId",
                table: "GameVersionRedistributables",
                column: "RedistributableId");

            // Until now every version shared the game's option schema and redistributables, so each
            // existing version starts with them
            migrationBuilder.Sql(@"UPDATE GameVersions v JOIN Games g ON g.Id = v.GameId SET v.OptionSchema = g.OptionSchema;");

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
