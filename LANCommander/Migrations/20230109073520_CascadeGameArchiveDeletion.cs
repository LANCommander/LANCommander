using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    public partial class CascadeGameArchiveDeletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Archive_Games_GameId",
                table: "Archive");

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_Games_GameId",
                table: "Archive",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Archive_Games_GameId",
                table: "Archive");

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_Games_GameId",
                table: "Archive",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id");
        }
    }
}
