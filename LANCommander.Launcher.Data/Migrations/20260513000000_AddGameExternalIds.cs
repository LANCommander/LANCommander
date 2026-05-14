using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Launcher.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameExternalIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameExternalIds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ImportedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameExternalIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameExternalIds_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameExternalIds_GameId",
                table: "GameExternalIds",
                column: "GameId");

            // Migrate existing IGDBId data
            migrationBuilder.Sql(@"
                INSERT INTO GameExternalIds (Id, GameId, Provider, ExternalId, CreatedOn, UpdatedOn)
                SELECT lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6))),
                       Id, 'IGDB', CAST(IGDBId AS TEXT), CreatedOn, UpdatedOn
                FROM Games
                WHERE IGDBId IS NOT NULL
            ");

            migrationBuilder.DropColumn(
                name: "IGDBId",
                table: "Games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "IGDBId",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            // Migrate data back
            migrationBuilder.Sql(@"
                UPDATE Games
                SET IGDBId = CAST((SELECT ExternalId FROM GameExternalIds WHERE GameExternalIds.GameId = Games.Id AND Provider = 'IGDB' LIMIT 1) AS INTEGER)
                WHERE Id IN (SELECT GameId FROM GameExternalIds WHERE Provider = 'IGDB')
            ");

            migrationBuilder.DropTable(
                name: "GameExternalIds");
        }
    }
}
