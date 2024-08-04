using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    public partial class CascadeGameKeyDeletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Keys_Games_GameId",
                table: "Keys");

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_Games_GameId",
                table: "Keys",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Keys_Games_GameId",
                table: "Keys");

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_Games_GameId",
                table: "Keys",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id");
        }
    }
}
