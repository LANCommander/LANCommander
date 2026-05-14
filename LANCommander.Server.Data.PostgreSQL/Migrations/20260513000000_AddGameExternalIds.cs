using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.PostgreSQL.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_GameExternalIds_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GameExternalIds_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameExternalIds_CreatedById",
                table: "GameExternalIds",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GameExternalIds_GameId",
                table: "GameExternalIds",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameExternalIds_UpdatedById",
                table: "GameExternalIds",
                column: "UpdatedById");

            // Migrate existing IGDBId data
            migrationBuilder.Sql(@"
                INSERT INTO ""GameExternalIds"" (""Id"", ""GameId"", ""Provider"", ""ExternalId"", ""CreatedOn"", ""UpdatedOn"")
                SELECT gen_random_uuid(), ""Id"", 'IGDB', CAST(""IGDBId"" AS TEXT), ""CreatedOn"", ""UpdatedOn""
                FROM ""Games""
                WHERE ""IGDBId"" IS NOT NULL
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
                type: "bigint",
                nullable: true);

            // Migrate data back
            migrationBuilder.Sql(@"
                UPDATE ""Games""
                SET ""IGDBId"" = CAST(e.""ExternalId"" AS BIGINT)
                FROM ""GameExternalIds"" e
                WHERE e.""GameId"" = ""Games"".""Id"" AND e.""Provider"" = 'IGDB'
            ");

            migrationBuilder.DropTable(
                name: "GameExternalIds");
        }
    }
}
