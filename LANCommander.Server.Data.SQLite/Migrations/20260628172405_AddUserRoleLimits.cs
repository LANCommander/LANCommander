using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DownloadSpeedKBps",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageQuotaMB",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadSpeedKBps",
                table: "Roles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageQuotaMB",
                table: "Roles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownloadSpeedKBps",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StorageQuotaMB",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DownloadSpeedKBps",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "StorageQuotaMB",
                table: "Roles");
        }
    }
}
