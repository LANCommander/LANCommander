using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class FixDeletionBehaviors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actions_AspNetUsers_CreatedById",
                table: "Actions");

            migrationBuilder.DropForeignKey(
                name: "FK_Actions_AspNetUsers_UpdatedById",
                table: "Actions");

            migrationBuilder.DropForeignKey(
                name: "FK_Archive_AspNetUsers_CreatedById",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_Archive_AspNetUsers_UpdatedById",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_CreatedById",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_UpdatedById",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_AspNetUsers_CreatedById",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_AspNetUsers_UpdatedById",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_AspNetUsers_CreatedById",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_AspNetUsers_UpdatedById",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_AspNetUsers_CreatedById",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_AspNetUsers_UpdatedById",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_Games_GameId",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_Genres_AspNetUsers_CreatedById",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_Genres_AspNetUsers_UpdatedById",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_Keys_AspNetUsers_CreatedById",
                table: "Keys");

            migrationBuilder.DropForeignKey(
                name: "FK_Keys_AspNetUsers_UpdatedById",
                table: "Keys");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_AspNetUsers_CreatedById",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_AspNetUsers_UpdatedById",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_CreatedById",
                table: "MultiplayerModes");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_UpdatedById",
                table: "MultiplayerModes");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_AspNetUsers_CreatedById",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_AspNetUsers_UpdatedById",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Games_GameId",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Redistributables_AspNetUsers_CreatedById",
                table: "Redistributables");

            migrationBuilder.DropForeignKey(
                name: "FK_Redistributables_AspNetUsers_UpdatedById",
                table: "Redistributables");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_AspNetUsers_CreatedById",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_AspNetUsers_UpdatedById",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_Games_GameId",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_AspNetUsers_CreatedById",
                table: "Scripts");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_AspNetUsers_UpdatedById",
                table: "Scripts");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_CreatedById",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_UpdatedById",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerHttpPath_AspNetUsers_CreatedById",
                table: "ServerHttpPath");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerHttpPath_AspNetUsers_UpdatedById",
                table: "ServerHttpPath");

            migrationBuilder.DropForeignKey(
                name: "FK_Servers_AspNetUsers_CreatedById",
                table: "Servers");

            migrationBuilder.DropForeignKey(
                name: "FK_Servers_AspNetUsers_UpdatedById",
                table: "Servers");

            migrationBuilder.DropForeignKey(
                name: "FK_Servers_Games_GameId",
                table: "Servers");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_AspNetUsers_CreatedById",
                table: "Tags");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_AspNetUsers_UpdatedById",
                table: "Tags");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "PlaySessions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "GameSaves",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_AspNetUsers_CreatedById",
                table: "Actions",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_AspNetUsers_UpdatedById",
                table: "Actions",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_AspNetUsers_CreatedById",
                table: "Archive",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_AspNetUsers_UpdatedById",
                table: "Archive",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_AspNetUsers_CreatedById",
                table: "Categories",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_AspNetUsers_UpdatedById",
                table: "Categories",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_AspNetUsers_CreatedById",
                table: "Companies",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_AspNetUsers_UpdatedById",
                table: "Companies",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_AspNetUsers_CreatedById",
                table: "Games",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_AspNetUsers_UpdatedById",
                table: "Games",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_AspNetUsers_CreatedById",
                table: "GameSaves",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_AspNetUsers_UpdatedById",
                table: "GameSaves",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_Games_GameId",
                table: "GameSaves",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_AspNetUsers_CreatedById",
                table: "Genres",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_AspNetUsers_UpdatedById",
                table: "Genres",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_AspNetUsers_CreatedById",
                table: "Keys",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_AspNetUsers_UpdatedById",
                table: "Keys",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_AspNetUsers_CreatedById",
                table: "Media",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_AspNetUsers_UpdatedById",
                table: "Media",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_CreatedById",
                table: "MultiplayerModes",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_UpdatedById",
                table: "MultiplayerModes",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_AspNetUsers_CreatedById",
                table: "PlaySessions",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_AspNetUsers_UpdatedById",
                table: "PlaySessions",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Games_GameId",
                table: "PlaySessions",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Redistributables_AspNetUsers_CreatedById",
                table: "Redistributables",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Redistributables_AspNetUsers_UpdatedById",
                table: "Redistributables",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_AspNetUsers_CreatedById",
                table: "SavePaths",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_AspNetUsers_UpdatedById",
                table: "SavePaths",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_Games_GameId",
                table: "SavePaths",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_AspNetUsers_CreatedById",
                table: "Scripts",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_AspNetUsers_UpdatedById",
                table: "Scripts",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_CreatedById",
                table: "ServerConsoles",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_UpdatedById",
                table: "ServerConsoles",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerHttpPath_AspNetUsers_CreatedById",
                table: "ServerHttpPath",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerHttpPath_AspNetUsers_UpdatedById",
                table: "ServerHttpPath",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_AspNetUsers_CreatedById",
                table: "Servers",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_AspNetUsers_UpdatedById",
                table: "Servers",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_Games_GameId",
                table: "Servers",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_AspNetUsers_CreatedById",
                table: "Tags",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_AspNetUsers_UpdatedById",
                table: "Tags",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actions_AspNetUsers_CreatedById",
                table: "Actions");

            migrationBuilder.DropForeignKey(
                name: "FK_Actions_AspNetUsers_UpdatedById",
                table: "Actions");

            migrationBuilder.DropForeignKey(
                name: "FK_Archive_AspNetUsers_CreatedById",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_Archive_AspNetUsers_UpdatedById",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_CreatedById",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_UpdatedById",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_AspNetUsers_CreatedById",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_AspNetUsers_UpdatedById",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_AspNetUsers_CreatedById",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_AspNetUsers_UpdatedById",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_AspNetUsers_CreatedById",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_AspNetUsers_UpdatedById",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_Games_GameId",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_Genres_AspNetUsers_CreatedById",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_Genres_AspNetUsers_UpdatedById",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_Keys_AspNetUsers_CreatedById",
                table: "Keys");

            migrationBuilder.DropForeignKey(
                name: "FK_Keys_AspNetUsers_UpdatedById",
                table: "Keys");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_AspNetUsers_CreatedById",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_AspNetUsers_UpdatedById",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_CreatedById",
                table: "MultiplayerModes");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_UpdatedById",
                table: "MultiplayerModes");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_AspNetUsers_CreatedById",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_AspNetUsers_UpdatedById",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Games_GameId",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Redistributables_AspNetUsers_CreatedById",
                table: "Redistributables");

            migrationBuilder.DropForeignKey(
                name: "FK_Redistributables_AspNetUsers_UpdatedById",
                table: "Redistributables");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_AspNetUsers_CreatedById",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_AspNetUsers_UpdatedById",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_Games_GameId",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_AspNetUsers_CreatedById",
                table: "Scripts");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_AspNetUsers_UpdatedById",
                table: "Scripts");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_CreatedById",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_UpdatedById",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerHttpPath_AspNetUsers_CreatedById",
                table: "ServerHttpPath");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerHttpPath_AspNetUsers_UpdatedById",
                table: "ServerHttpPath");

            migrationBuilder.DropForeignKey(
                name: "FK_Servers_AspNetUsers_CreatedById",
                table: "Servers");

            migrationBuilder.DropForeignKey(
                name: "FK_Servers_AspNetUsers_UpdatedById",
                table: "Servers");

            migrationBuilder.DropForeignKey(
                name: "FK_Servers_Games_GameId",
                table: "Servers");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_AspNetUsers_CreatedById",
                table: "Tags");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_AspNetUsers_UpdatedById",
                table: "Tags");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "PlaySessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "GameSaves",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_AspNetUsers_CreatedById",
                table: "Actions",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_AspNetUsers_UpdatedById",
                table: "Actions",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_AspNetUsers_CreatedById",
                table: "Archive",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_AspNetUsers_UpdatedById",
                table: "Archive",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_AspNetUsers_CreatedById",
                table: "Categories",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_AspNetUsers_UpdatedById",
                table: "Categories",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_AspNetUsers_CreatedById",
                table: "Companies",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_AspNetUsers_UpdatedById",
                table: "Companies",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_AspNetUsers_CreatedById",
                table: "Games",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_AspNetUsers_UpdatedById",
                table: "Games",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_AspNetUsers_CreatedById",
                table: "GameSaves",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_AspNetUsers_UpdatedById",
                table: "GameSaves",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_Games_GameId",
                table: "GameSaves",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_AspNetUsers_CreatedById",
                table: "Genres",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_AspNetUsers_UpdatedById",
                table: "Genres",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_AspNetUsers_CreatedById",
                table: "Keys",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_AspNetUsers_UpdatedById",
                table: "Keys",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_AspNetUsers_CreatedById",
                table: "Media",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_AspNetUsers_UpdatedById",
                table: "Media",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_CreatedById",
                table: "MultiplayerModes",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_UpdatedById",
                table: "MultiplayerModes",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_AspNetUsers_CreatedById",
                table: "PlaySessions",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_AspNetUsers_UpdatedById",
                table: "PlaySessions",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Games_GameId",
                table: "PlaySessions",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Redistributables_AspNetUsers_CreatedById",
                table: "Redistributables",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Redistributables_AspNetUsers_UpdatedById",
                table: "Redistributables",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_AspNetUsers_CreatedById",
                table: "SavePaths",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_AspNetUsers_UpdatedById",
                table: "SavePaths",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_Games_GameId",
                table: "SavePaths",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_AspNetUsers_CreatedById",
                table: "Scripts",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_AspNetUsers_UpdatedById",
                table: "Scripts",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_CreatedById",
                table: "ServerConsoles",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_UpdatedById",
                table: "ServerConsoles",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerHttpPath_AspNetUsers_CreatedById",
                table: "ServerHttpPath",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerHttpPath_AspNetUsers_UpdatedById",
                table: "ServerHttpPath",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_AspNetUsers_CreatedById",
                table: "Servers",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_AspNetUsers_UpdatedById",
                table: "Servers",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_Games_GameId",
                table: "Servers",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_AspNetUsers_CreatedById",
                table: "Tags",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_AspNetUsers_UpdatedById",
                table: "Tags",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
