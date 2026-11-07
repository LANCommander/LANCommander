using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
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
                type: "int",
                nullable: false,
                defaultValue: 0);

            // A primary action becomes ActionType.Primary (1); every other one stays ActionType.Secondary (0)
            migrationBuilder.Sql("UPDATE `Actions` SET `Type` = 1 WHERE `PrimaryAction` = 1");

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
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE `Actions` SET `PrimaryAction` = 1 WHERE `Type` = 1");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Actions");
        }
    }
}
