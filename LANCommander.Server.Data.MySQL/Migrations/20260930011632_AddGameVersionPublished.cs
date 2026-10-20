using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddGameVersionPublished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "GameVersions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Versions of a game already offered to launchers stay offered; a hidden game's versions start as drafts
            migrationBuilder.Sql(@"UPDATE GameVersions v JOIN Games g ON g.Id = v.GameId SET v.Published = g.Published;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Published",
                table: "GameVersions");
        }
    }
}
