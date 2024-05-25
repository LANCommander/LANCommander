using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Client.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameLatestVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LatestVersion",
                table: "Games",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestVersion",
                table: "Games");
        }
    }
}
