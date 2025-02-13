using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class FixMediaDeletionsAndServerFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE ServerHttpPath SET ServerId = ServerId1 WHERE ServerId IS NULL");
            migrationBuilder.Sql("UPDATE ServerConsoles SET ServerId = ServerId1 WHERE ServerId IS NULL");
            
            migrationBuilder.DropForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_Servers_ServerId1",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerHttpPath_Servers_ServerId1",
                table: "ServerHttpPath");

            migrationBuilder.DropIndex(
                name: "IX_ServerHttpPath_ServerId1",
                table: "ServerHttpPath");

            migrationBuilder.DropIndex(
                name: "IX_ServerConsoles_ServerId1",
                table: "ServerConsoles");

            migrationBuilder.DropColumn(
                name: "ServerId1",
                table: "ServerHttpPath");

            migrationBuilder.DropColumn(
                name: "ServerId1",
                table: "ServerConsoles");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media");

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId1",
                table: "ServerHttpPath",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId1",
                table: "ServerConsoles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerHttpPath_ServerId1",
                table: "ServerHttpPath",
                column: "ServerId1");

            migrationBuilder.CreateIndex(
                name: "IX_ServerConsoles_ServerId1",
                table: "ServerConsoles",
                column: "ServerId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_StorageLocations_StorageLocationId",
                table: "Media",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConsoles_Servers_ServerId1",
                table: "ServerConsoles",
                column: "ServerId1",
                principalTable: "Servers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerHttpPath_Servers_ServerId1",
                table: "ServerHttpPath",
                column: "ServerId1",
                principalTable: "Servers",
                principalColumn: "Id");
        }
    }
}
