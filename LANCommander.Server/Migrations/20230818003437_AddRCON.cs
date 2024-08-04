using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddRCON : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServerLog_AspNetUsers_CreatedById",
                table: "ServerLog");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerLog_AspNetUsers_UpdatedById",
                table: "ServerLog");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerLog_Servers_ServerId",
                table: "ServerLog");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerLog_Servers_ServerId1",
                table: "ServerLog");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ServerLog",
                table: "ServerLog");

            migrationBuilder.RenameTable(
                name: "ServerLog",
                newName: "ServerConsoles");

            migrationBuilder.RenameIndex(
                name: "IX_ServerLog_UpdatedById",
                table: "ServerConsoles",
                newName: "IX_ServerConsoles_UpdatedById");

            migrationBuilder.RenameIndex(
                name: "IX_ServerLog_ServerId1",
                table: "ServerConsoles",
                newName: "IX_ServerConsoles_ServerId1");

            migrationBuilder.RenameIndex(
                name: "IX_ServerLog_ServerId",
                table: "ServerConsoles",
                newName: "IX_ServerConsoles_ServerId");

            migrationBuilder.RenameIndex(
                name: "IX_ServerLog_CreatedById",
                table: "ServerConsoles",
                newName: "IX_ServerConsoles_CreatedById");

            migrationBuilder.AddColumn<string>(
                name: "Host",
                table: "ServerConsoles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "ServerConsoles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Port",
                table: "ServerConsoles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ServerConsoles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServerConsoles",
                table: "ServerConsoles",
                column: "Id");

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
                name: "FK_ServerConsoles_Servers_ServerId",
                table: "ServerConsoles",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConsoles_Servers_ServerId1",
                table: "ServerConsoles",
                column: "ServerId1",
                principalTable: "Servers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_CreatedById",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_AspNetUsers_UpdatedById",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_Servers_ServerId",
                table: "ServerConsoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConsoles_Servers_ServerId1",
                table: "ServerConsoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ServerConsoles",
                table: "ServerConsoles");

            migrationBuilder.DropColumn(
                name: "Host",
                table: "ServerConsoles");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "ServerConsoles");

            migrationBuilder.DropColumn(
                name: "Port",
                table: "ServerConsoles");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ServerConsoles");

            migrationBuilder.RenameTable(
                name: "ServerConsoles",
                newName: "ServerLog");

            migrationBuilder.RenameIndex(
                name: "IX_ServerConsoles_UpdatedById",
                table: "ServerLog",
                newName: "IX_ServerLog_UpdatedById");

            migrationBuilder.RenameIndex(
                name: "IX_ServerConsoles_ServerId1",
                table: "ServerLog",
                newName: "IX_ServerLog_ServerId1");

            migrationBuilder.RenameIndex(
                name: "IX_ServerConsoles_ServerId",
                table: "ServerLog",
                newName: "IX_ServerLog_ServerId");

            migrationBuilder.RenameIndex(
                name: "IX_ServerConsoles_CreatedById",
                table: "ServerLog",
                newName: "IX_ServerLog_CreatedById");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServerLog",
                table: "ServerLog",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerLog_AspNetUsers_CreatedById",
                table: "ServerLog",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerLog_AspNetUsers_UpdatedById",
                table: "ServerLog",
                column: "UpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerLog_Servers_ServerId",
                table: "ServerLog",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerLog_Servers_ServerId1",
                table: "ServerLog",
                column: "ServerId1",
                principalTable: "Servers",
                principalColumn: "Id");
        }
    }
}
