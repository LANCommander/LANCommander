using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddServerActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "Actions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "Actions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Actions_ServerId",
                table: "Actions",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_Servers_ServerId",
                table: "Actions",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actions_Servers_ServerId",
                table: "Actions");

            migrationBuilder.DropIndex(
                name: "IX_Actions_ServerId",
                table: "Actions");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Actions");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "Actions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
