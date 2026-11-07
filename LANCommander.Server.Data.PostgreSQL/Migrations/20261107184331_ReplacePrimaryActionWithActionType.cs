using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePrimaryActionWithActionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Actions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // A primary action becomes ActionType.Primary (1); every other one stays ActionType.Secondary (0)
            migrationBuilder.Sql("UPDATE \"Actions\" SET \"Type\" = 1 WHERE \"PrimaryAction\"");

            migrationBuilder.DropColumn(
                name: "PrimaryAction",
                table: "Actions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrimaryAction",
                table: "Actions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE \"Actions\" SET \"PrimaryAction\" = TRUE WHERE \"Type\" = 1");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Actions");
        }
    }
}
