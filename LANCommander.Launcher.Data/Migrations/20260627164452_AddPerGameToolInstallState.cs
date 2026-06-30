using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Launcher.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerGameToolInstallState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameTool_Games_GamesId",
                table: "GameTool");

            migrationBuilder.DropForeignKey(
                name: "FK_GameTool_Tools_ToolsId",
                table: "GameTool");

            migrationBuilder.RenameColumn(
                name: "ToolsId",
                table: "GameTool",
                newName: "ToolId");

            migrationBuilder.RenameColumn(
                name: "GamesId",
                table: "GameTool",
                newName: "GameId");

            migrationBuilder.RenameIndex(
                name: "IX_GameTool_ToolsId",
                table: "GameTool",
                newName: "IX_GameTool_ToolId");

            migrationBuilder.AddColumn<string>(
                name: "InstallDirectory",
                table: "GameTool",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Installed",
                table: "GameTool",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "InstalledOn",
                table: "GameTool",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstalledVersion",
                table: "GameTool",
                type: "TEXT",
                nullable: true);

            // Tool install state was previously tracked globally on the Tools table. Migrate it onto
            // the join row for the game the tool was actually installed under (the game whose install
            // directory matches the tool's recorded directory) before the old columns are dropped.
            migrationBuilder.Sql(@"
                UPDATE GameTool
                SET
                    Installed = 1,
                    InstallDirectory = (SELECT t.InstallDirectory FROM Tools t WHERE t.Id = GameTool.ToolId),
                    InstalledVersion = (SELECT t.InstalledVersion FROM Tools t WHERE t.Id = GameTool.ToolId),
                    InstalledOn = (SELECT t.InstalledOn FROM Tools t WHERE t.Id = GameTool.ToolId)
                WHERE EXISTS (
                    SELECT 1
                    FROM Tools t
                    JOIN Games g ON g.Id = GameTool.GameId
                    WHERE t.Id = GameTool.ToolId
                      AND t.Installed = 1
                      AND t.InstallDirectory IS NOT NULL
                      AND t.InstallDirectory = g.InstallDirectory
                );");

            migrationBuilder.DropColumn(
                name: "InstallDirectory",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "Installed",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "InstalledOn",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "InstalledVersion",
                table: "Tools");

            migrationBuilder.AddForeignKey(
                name: "FK_GameTool_Games_GameId",
                table: "GameTool",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameTool_Tools_ToolId",
                table: "GameTool",
                column: "ToolId",
                principalTable: "Tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameTool_Games_GameId",
                table: "GameTool");

            migrationBuilder.DropForeignKey(
                name: "FK_GameTool_Tools_ToolId",
                table: "GameTool");

            migrationBuilder.DropColumn(
                name: "InstallDirectory",
                table: "GameTool");

            migrationBuilder.DropColumn(
                name: "Installed",
                table: "GameTool");

            migrationBuilder.DropColumn(
                name: "InstalledOn",
                table: "GameTool");

            migrationBuilder.DropColumn(
                name: "InstalledVersion",
                table: "GameTool");

            migrationBuilder.RenameColumn(
                name: "ToolId",
                table: "GameTool",
                newName: "ToolsId");

            migrationBuilder.RenameColumn(
                name: "GameId",
                table: "GameTool",
                newName: "GamesId");

            migrationBuilder.RenameIndex(
                name: "IX_GameTool_ToolId",
                table: "GameTool",
                newName: "IX_GameTool_ToolsId");

            migrationBuilder.AddColumn<string>(
                name: "InstallDirectory",
                table: "Tools",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Installed",
                table: "Tools",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "InstalledOn",
                table: "Tools",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstalledVersion",
                table: "Tools",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GameTool_Games_GamesId",
                table: "GameTool",
                column: "GamesId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameTool_Tools_ToolsId",
                table: "GameTool",
                column: "ToolsId",
                principalTable: "Tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
