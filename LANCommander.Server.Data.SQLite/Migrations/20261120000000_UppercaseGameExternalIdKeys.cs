using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <summary>
    /// AddGameExternalIds generated the ids of migrated IGDB links in lowercase. EF Core's SQLite
    /// provider writes Guid parameters as uppercase text, so any UPDATE or DELETE of those rows
    /// (e.g. replacing a game's external ids on import or save) matched nothing and failed as a
    /// concurrency error.
    /// </summary>
    public partial class UppercaseGameExternalIdKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE GameExternalIds SET Id = upper(Id) WHERE Id <> upper(Id)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
