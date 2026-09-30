using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.PostgreSQL.Migrations
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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Versions of a game already offered to launchers stay offered; a hidden game's versions start as drafts
            migrationBuilder.Sql(@"UPDATE ""GameVersions"" v SET ""Published"" = g.""Published"" FROM ""Games"" g WHERE g.""Id"" = v.""GameId"";");
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
