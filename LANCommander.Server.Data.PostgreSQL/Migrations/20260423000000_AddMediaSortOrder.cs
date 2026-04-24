using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Media",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Media");
        }
    }
}
