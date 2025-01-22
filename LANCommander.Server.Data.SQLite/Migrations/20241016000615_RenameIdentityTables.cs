using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class RenameIdentityTables : Migration
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
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_CreatedById",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_UpdatedById",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Collections_AspNetUsers_CreatedById",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_Collections_AspNetUsers_UpdatedById",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_AspNetUsers_CreatedById",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_AspNetUsers_UpdatedById",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Engines_AspNetUsers_CreatedById",
                table: "Engines");

            migrationBuilder.DropForeignKey(
                name: "FK_Engines_AspNetUsers_UpdatedById",
                table: "Engines");

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
                name: "FK_GameSaves_AspNetUsers_UserId",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_Genres_AspNetUsers_CreatedById",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_Genres_AspNetUsers_UpdatedById",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_AspNetUsers_CreatedById",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_AspNetUsers_ResolvedById",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_AspNetUsers_UpdatedById",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Games_GameId",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Keys_AspNetUsers_ClaimedByUserId",
                table: "Keys");

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
                name: "FK_Media_AspNetUsers_UserId",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_CreatedById",
                table: "MultiplayerModes");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiplayerModes_AspNetUsers_UpdatedById",
                table: "MultiplayerModes");

            migrationBuilder.DropForeignKey(
                name: "FK_Pages_AspNetUsers_CreatedById",
                table: "Pages");

            migrationBuilder.DropForeignKey(
                name: "FK_Pages_AspNetUsers_UpdatedById",
                table: "Pages");

            migrationBuilder.DropForeignKey(
                name: "FK_Platforms_AspNetUsers_CreatedById",
                table: "Platforms");

            migrationBuilder.DropForeignKey(
                name: "FK_Platforms_AspNetUsers_UpdatedById",
                table: "Platforms");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_AspNetUsers_CreatedById",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_AspNetUsers_UpdatedById",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_AspNetUsers_UserId",
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
                name: "FK_RoleCollection_AspNetRoles_RoleId",
                table: "RoleCollection");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_AspNetUsers_CreatedById",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_AspNetUsers_UpdatedById",
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
                name: "FK_StorageLocations_AspNetUsers_CreatedById",
                table: "StorageLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageLocations_AspNetUsers_UpdatedById",
                table: "StorageLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_AspNetUsers_CreatedById",
                table: "Tags");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_AspNetUsers_UpdatedById",
                table: "Tags");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCustomField_AspNetUsers_CreatedById",
                table: "UserCustomField");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCustomField_AspNetUsers_UpdatedById",
                table: "UserCustomField");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCustomField_AspNetUsers_UserId",
                table: "UserCustomField");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserTokens",
                table: "AspNetUserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserRoles",
                table: "AspNetUserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserLogins",
                table: "AspNetUserLogins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserClaims",
                table: "AspNetUserClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoleClaims",
                table: "AspNetRoleClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUsers",
                table: "AspNetUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "UserTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "UserRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "UserLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "UserClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "RoleClaims");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "Roles");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "UserRoles",
                newName: "IX_UserRoles_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "UserLogins",
                newName: "IX_UserLogins_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "UserClaims",
                newName: "IX_UserClaims_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "RoleClaims",
                newName: "IX_RoleClaims_RoleId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RefreshTokenExpiration",
                table: "Users",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<bool>(
                name: "Approved",
                table: "Users",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Users",
                type: "TEXT",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Roles",
                type: "TEXT",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserTokens",
                table: "UserTokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserLogins",
                table: "UserLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserClaims",
                table: "UserClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RoleClaims",
                table: "RoleClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                table: "Roles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_Users_CreatedById",
                table: "Actions",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_Users_UpdatedById",
                table: "Actions",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_Users_CreatedById",
                table: "Archive",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_Users_UpdatedById",
                table: "Archive",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_CreatedById",
                table: "Categories",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_UpdatedById",
                table: "Categories",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Users_CreatedById",
                table: "Collections",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Users_UpdatedById",
                table: "Collections",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_CreatedById",
                table: "Companies",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_UpdatedById",
                table: "Companies",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Engines_Users_CreatedById",
                table: "Engines",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Engines_Users_UpdatedById",
                table: "Engines",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_CreatedById",
                table: "Games",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_UpdatedById",
                table: "Games",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_Users_CreatedById",
                table: "GameSaves",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_Users_UpdatedById",
                table: "GameSaves",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSaves_Users_UserId",
                table: "GameSaves",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_Users_CreatedById",
                table: "Genres",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_Users_UpdatedById",
                table: "Genres",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Games_GameId",
                table: "Issues",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Users_CreatedById",
                table: "Issues",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Users_ResolvedById",
                table: "Issues",
                column: "ResolvedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Users_UpdatedById",
                table: "Issues",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_Users_ClaimedByUserId",
                table: "Keys",
                column: "ClaimedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_Users_CreatedById",
                table: "Keys",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_Users_UpdatedById",
                table: "Keys",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_Users_CreatedById",
                table: "Media",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_Users_UpdatedById",
                table: "Media",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_Users_UserId",
                table: "Media",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MultiplayerModes_Users_CreatedById",
                table: "MultiplayerModes",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MultiplayerModes_Users_UpdatedById",
                table: "MultiplayerModes",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_Users_CreatedById",
                table: "Pages",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_Users_UpdatedById",
                table: "Pages",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Platforms_Users_CreatedById",
                table: "Platforms",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Platforms_Users_UpdatedById",
                table: "Platforms",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Games_GameId",
                table: "PlaySessions",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Users_CreatedById",
                table: "PlaySessions",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Users_UpdatedById",
                table: "PlaySessions",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Users_UserId",
                table: "PlaySessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Redistributables_Users_CreatedById",
                table: "Redistributables",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Redistributables_Users_UpdatedById",
                table: "Redistributables",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleClaims_Roles_RoleId",
                table: "RoleClaims",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleCollection_Roles_RoleId",
                table: "RoleCollection",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_Users_CreatedById",
                table: "SavePaths",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SavePaths_Users_UpdatedById",
                table: "SavePaths",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_Users_CreatedById",
                table: "Scripts",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_Users_UpdatedById",
                table: "Scripts",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConsoles_Users_CreatedById",
                table: "ServerConsoles",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConsoles_Users_UpdatedById",
                table: "ServerConsoles",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerHttpPath_Users_CreatedById",
                table: "ServerHttpPath",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerHttpPath_Users_UpdatedById",
                table: "ServerHttpPath",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_Users_CreatedById",
                table: "Servers",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Servers_Users_UpdatedById",
                table: "Servers",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageLocations_Users_CreatedById",
                table: "StorageLocations",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StorageLocations_Users_UpdatedById",
                table: "StorageLocations",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Users_CreatedById",
                table: "Tags",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Users_UpdatedById",
                table: "Tags",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserClaims_Users_UserId",
                table: "UserClaims",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCustomField_Users_CreatedById",
                table: "UserCustomField",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCustomField_Users_UpdatedById",
                table: "UserCustomField",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCustomField_Users_UserId",
                table: "UserCustomField",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserLogins_Users_UserId",
                table: "UserLogins",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTokens_Users_UserId",
                table: "UserTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actions_Users_CreatedById",
                table: "Actions");

            migrationBuilder.DropForeignKey(
                name: "FK_Actions_Users_UpdatedById",
                table: "Actions");

            migrationBuilder.DropForeignKey(
                name: "FK_Archive_Users_CreatedById",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_Archive_Users_UpdatedById",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Users_CreatedById",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Users_UpdatedById",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Users_CreatedById",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Users_UpdatedById",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_CreatedById",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_UpdatedById",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Engines_Users_CreatedById",
                table: "Engines");

            migrationBuilder.DropForeignKey(
                name: "FK_Engines_Users_UpdatedById",
                table: "Engines");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_CreatedById",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_UpdatedById",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_Users_CreatedById",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_Users_UpdatedById",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSaves_Users_UserId",
                table: "GameSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_Genres_Users_CreatedById",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_Genres_Users_UpdatedById",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Games_GameId",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Users_CreatedById",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Users_ResolvedById",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Users_UpdatedById",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Keys_Users_ClaimedByUserId",
                table: "Keys");

            migrationBuilder.DropForeignKey(
                name: "FK_Keys_Users_CreatedById",
                table: "Keys");

            migrationBuilder.DropForeignKey(
                name: "FK_Keys_Users_UpdatedById",
                table: "Keys");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_Users_CreatedById",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_Users_UpdatedById",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_Users_UserId",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiplayerModes_Users_CreatedById",
                table: "MultiplayerModes");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiplayerModes_Users_UpdatedById",
                table: "MultiplayerModes");

            migrationBuilder.DropForeignKey(
                name: "FK_Pages_Users_CreatedById",
                table: "Pages");

            migrationBuilder.DropForeignKey(
                name: "FK_Pages_Users_UpdatedById",
                table: "Pages");

            migrationBuilder.DropForeignKey(
                name: "FK_Platforms_Users_CreatedById",
                table: "Platforms");

            migrationBuilder.DropForeignKey(
                name: "FK_Platforms_Users_UpdatedById",
                table: "Platforms");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Games_GameId",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Users_CreatedById",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Users_UpdatedById",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Users_UserId",
                table: "PlaySessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Redistributables_Users_CreatedById",
                table: "Redistributables");

            migrationBuilder.DropForeignKey(
                name: "FK_Redistributables_Users_UpdatedById",
                table: "Redistributables");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleClaims_Roles_RoleId",
                table: "RoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleCollection_Roles_RoleId",
                table: "RoleCollection");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_Users_CreatedById",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_SavePaths_Users_UpdatedById",
                table: "SavePaths");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_Users_CreatedById",
                table: "Scripts");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_Users_UpdatedById",
                table: "Scripts");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_Users_CreatedById",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_Users_UpdatedById",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerHttpPath_Users_CreatedById",
                table: "ServerHttpPath");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerHttpPath_Users_UpdatedById",
                table: "ServerHttpPath");

            migrationBuilder.DropForeignKey(
                name: "FK_Servers_Users_CreatedById",
                table: "Servers");

            migrationBuilder.DropForeignKey(
                name: "FK_Servers_Users_UpdatedById",
                table: "Servers");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageLocations_Users_CreatedById",
                table: "StorageLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageLocations_Users_UpdatedById",
                table: "StorageLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Users_CreatedById",
                table: "Tags");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Users_UpdatedById",
                table: "Tags");

            migrationBuilder.DropForeignKey(
                name: "FK_UserClaims_Users_UserId",
                table: "UserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCustomField_Users_CreatedById",
                table: "UserCustomField");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCustomField_Users_UpdatedById",
                table: "UserCustomField");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCustomField_Users_UserId",
                table: "UserCustomField");

            migrationBuilder.DropForeignKey(
                name: "FK_UserLogins_Users_UserId",
                table: "UserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTokens_Users_UserId",
                table: "UserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserTokens",
                table: "UserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserLogins",
                table: "UserLogins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserClaims",
                table: "UserClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RoleClaims",
                table: "RoleClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Roles");

            migrationBuilder.RenameTable(
                name: "UserTokens",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "UserLogins",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "UserClaims",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "RoleClaims",
                newName: "AspNetRoleClaims");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "AspNetRoles");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoles_RoleId",
                table: "AspNetUserRoles",
                newName: "IX_AspNetUserRoles_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_UserLogins_UserId",
                table: "AspNetUserLogins",
                newName: "IX_AspNetUserLogins_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserClaims_UserId",
                table: "AspNetUserClaims",
                newName: "IX_AspNetUserClaims_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_RoleClaims_RoleId",
                table: "AspNetRoleClaims",
                newName: "IX_AspNetRoleClaims_RoleId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RefreshTokenExpiration",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Approved",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserTokens",
                table: "AspNetUserTokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserRoles",
                table: "AspNetUserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserLogins",
                table: "AspNetUserLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserClaims",
                table: "AspNetUserClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoleClaims",
                table: "AspNetRoleClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUsers",
                table: "AspNetUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles",
                column: "Id");

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
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_Collections_AspNetUsers_CreatedById",
                table: "Collections",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_AspNetUsers_UpdatedById",
                table: "Collections",
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
                name: "FK_Engines_AspNetUsers_CreatedById",
                table: "Engines",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Engines_AspNetUsers_UpdatedById",
                table: "Engines",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

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
                name: "FK_GameSaves_AspNetUsers_UserId",
                table: "GameSaves",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_Issues_AspNetUsers_CreatedById",
                table: "Issues",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_AspNetUsers_ResolvedById",
                table: "Issues",
                column: "ResolvedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_AspNetUsers_UpdatedById",
                table: "Issues",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Games_GameId",
                table: "Issues",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Keys_AspNetUsers_ClaimedByUserId",
                table: "Keys",
                column: "ClaimedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

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
                name: "FK_Media_AspNetUsers_UserId",
                table: "Media",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_Pages_AspNetUsers_CreatedById",
                table: "Pages",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_AspNetUsers_UpdatedById",
                table: "Pages",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Platforms_AspNetUsers_CreatedById",
                table: "Platforms",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Platforms_AspNetUsers_UpdatedById",
                table: "Platforms",
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
                name: "FK_PlaySessions_AspNetUsers_UserId",
                table: "PlaySessions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_RoleCollection_AspNetRoles_RoleId",
                table: "RoleCollection",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_StorageLocations_AspNetUsers_CreatedById",
                table: "StorageLocations",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StorageLocations_AspNetUsers_UpdatedById",
                table: "StorageLocations",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_UserCustomField_AspNetUsers_CreatedById",
                table: "UserCustomField",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCustomField_AspNetUsers_UpdatedById",
                table: "UserCustomField",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCustomField_AspNetUsers_UserId",
                table: "UserCustomField",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
