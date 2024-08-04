using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddServerScripts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "Scripts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scripts_ServerId",
                table: "Scripts",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_Servers_ServerId",
                table: "Scripts",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_Servers_ServerId",
                table: "Scripts");

            migrationBuilder.DropIndex(
                name: "IX_Scripts_ServerId",
                table: "Scripts");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Scripts");
        }
    }
}
