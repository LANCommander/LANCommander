using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class ChatReadStatusMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatThreadReadStatuses_ThreadId",
                table: "ChatThreadReadStatuses");

            migrationBuilder.RenameColumn(
                name: "LastReadOn",
                table: "ChatThreadReadStatuses",
                newName: "LastReadMessageId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatThreadReadStatuses",
                table: "ChatThreadReadStatuses",
                columns: new[] { "ThreadId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreadReadStatuses_LastReadMessageId",
                table: "ChatThreadReadStatuses",
                column: "LastReadMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatThreadReadStatuses_ChatMessages_LastReadMessageId",
                table: "ChatThreadReadStatuses",
                column: "LastReadMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatThreadReadStatuses_ChatMessages_LastReadMessageId",
                table: "ChatThreadReadStatuses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatThreadReadStatuses",
                table: "ChatThreadReadStatuses");

            migrationBuilder.DropIndex(
                name: "IX_ChatThreadReadStatuses_LastReadMessageId",
                table: "ChatThreadReadStatuses");

            migrationBuilder.RenameColumn(
                name: "LastReadMessageId",
                table: "ChatThreadReadStatuses",
                newName: "LastReadOn");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreadReadStatuses_ThreadId",
                table: "ChatThreadReadStatuses",
                column: "ThreadId");
        }
    }
}
