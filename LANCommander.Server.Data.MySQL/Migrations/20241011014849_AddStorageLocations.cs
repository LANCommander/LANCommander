using System;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StorageLocationId",
                table: "Media",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "StorageLocationId",
                table: "GameSaves",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "StorageLocationId",
                table: "Archive",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "StorageLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Default = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Path = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedOn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageLocations_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StorageLocations_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Media_StorageLocationId",
                table: "Media",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSaves_StorageLocationId",
                table: "GameSaves",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Archive_StorageLocationId",
                table: "Archive",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_CreatedById",
                table: "StorageLocations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_UpdatedById",
                table: "StorageLocations",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_StorageLocations_StorageLocationId",
                table: "Archive",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_StorageLocations_StorageLocationId",
                table: "GameSaves",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql($"INSERT INTO StorageLocations (Id, Path, Type, `Default`, CreatedOn, UpdatedOn) VALUES (upper(UUID()), 'Uploads', {(int)StorageLocationType.Archive}, 1, NOW(), NOW())");
            migrationBuilder.Sql($"INSERT INTO StorageLocations (Id, Path, Type, `Default`, CreatedOn, UpdatedOn) VALUES (upper(UUID()), 'Saves', {(int)StorageLocationType.Save}, 1, NOW(), NOW())");
            migrationBuilder.Sql($"INSERT INTO StorageLocations (Id, Path, Type, `Default`, CreatedOn, UpdatedOn) VALUES (upper(UUID()), 'Media', {(int)StorageLocationType.Media}, 1, NOW(), NOW())");

            migrationBuilder.Sql($@"UPDATE Archive
                SET StorageLocationId = (
                    SELECT Id
                    FROM StorageLocations
                    WHERE Type = {(int)StorageLocationType.Archive}
                    LIMIT 1
                );");

                        migrationBuilder.Sql($@"UPDATE GameSaves
                SET StorageLocationId = (
                    SELECT Id
                    FROM StorageLocations
                    WHERE Type = {(int)StorageLocationType.Save}
                    LIMIT 1
                );");

                        migrationBuilder.Sql($@"UPDATE Media
                SET StorageLocationId = (
                    SELECT Id
                    FROM StorageLocations
                    WHERE Type = {(int)StorageLocationType.Media}
                    LIMIT 1
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Archive_StorageLocations_StorageLocationId",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_StorageLocations_StorageLocationId",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media");

            migrationBuilder.DropTable(
                name: "StorageLocations");

            migrationBuilder.DropIndex(
                name: "IX_Media_StorageLocationId",
                table: "Media");

            migrationBuilder.DropIndex(
                name: "IX_GameSaves_StorageLocationId",
                table: "GameSaves");

            migrationBuilder.DropIndex(
                name: "IX_Archive_StorageLocationId",
                table: "Archive");

            migrationBuilder.DropColumn(
                name: "StorageLocationId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "StorageLocationId",
                table: "GameSaves");

            migrationBuilder.DropColumn(
                name: "StorageLocationId",
                table: "Archive");
        }
    }
}
