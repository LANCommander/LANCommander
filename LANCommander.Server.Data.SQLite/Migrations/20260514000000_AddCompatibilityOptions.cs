using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddCompatibilityOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OptionSchema",
                table: "Redistributables",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Options",
                table: "GameRedistributable",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionOverrides",
                table: "Actions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptionSchema",
                table: "Redistributables");

            migrationBuilder.DropColumn(
                name: "Options",
                table: "GameRedistributable");

            migrationBuilder.DropColumn(
                name: "OptionOverrides",
                table: "Actions");
        }
    }
}
