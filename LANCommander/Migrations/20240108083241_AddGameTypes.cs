using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BaseGameId",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Games_BaseGameId",
                table: "Games",
                column: "BaseGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Games_BaseGameId",
                table: "Games",
                column: "BaseGameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Games_BaseGameId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_BaseGameId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "BaseGameId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Games");
        }
    }
}
