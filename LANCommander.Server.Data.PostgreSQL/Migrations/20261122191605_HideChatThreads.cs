using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class HideChatThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Hidden",
                table: "ChatThreadReadStatuses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hidden",
                table: "ChatThreadReadStatuses");
        }
    }
}
