using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddGameVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    GameId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "GameVersionId",
                table: "Archive");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "Actions");
        }
    }
}
