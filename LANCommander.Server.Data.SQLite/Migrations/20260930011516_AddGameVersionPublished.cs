using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
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
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Versions of a game already offered to launchers stay offered; a hidden game's versions start as drafts
            migrationBuilder.Sql(@"UPDATE GameVersions SET Published = (SELECT g.Published FROM Games g WHERE g.Id = GameVersions.GameId);");
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
