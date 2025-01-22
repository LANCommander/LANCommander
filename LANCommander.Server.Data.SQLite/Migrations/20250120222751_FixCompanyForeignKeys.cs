using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class FixCompanyForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameDeveloper",
                table: "GameDeveloper");

            migrationBuilder.DropIndex(
                name: "IX_GameDeveloper_GameId",
                table: "GameDeveloper");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameDeveloper",
                table: "GameDeveloper",
                columns: new[] { "GameId", "DeveloperId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameDeveloper_DeveloperId",
                table: "GameDeveloper",
                column: "DeveloperId");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameDeveloper",
                table: "GameDeveloper");

            migrationBuilder.DropIndex(
                name: "IX_GameDeveloper_DeveloperId",
                table: "GameDeveloper");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameDeveloper",
                table: "GameDeveloper",
                columns: new[] { "DeveloperId", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameDeveloper_GameId",
                table: "GameDeveloper",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
