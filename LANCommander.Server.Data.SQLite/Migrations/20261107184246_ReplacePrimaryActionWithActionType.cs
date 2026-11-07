using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePrimaryActionWithActionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite keeps the flag as 0/1, which are already ActionType.Secondary and ActionType.Primary
            migrationBuilder.RenameColumn(
                name: "PrimaryAction",
                table: "Actions",
                newName: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Config and Join actions weren't primary; leave the flag a plain 0/1
            migrationBuilder.Sql("UPDATE \"Actions\" SET \"Type\" = 0 WHERE \"Type\" <> 1");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Actions",
                newName: "PrimaryAction");
        }
    }
}
