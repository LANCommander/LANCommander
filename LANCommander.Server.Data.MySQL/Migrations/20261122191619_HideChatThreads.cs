using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
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
                type: "tinyint(1)",
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
