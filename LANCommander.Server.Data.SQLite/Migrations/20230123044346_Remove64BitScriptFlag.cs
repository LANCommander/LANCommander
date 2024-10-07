using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    public partial class Remove64BitScriptFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Requires64Bit",
                table: "Scripts");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Requires64Bit",
                table: "Scripts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
