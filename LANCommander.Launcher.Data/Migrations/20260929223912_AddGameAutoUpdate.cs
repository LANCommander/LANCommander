using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Launcher.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameAutoUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoUpdate",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstalledVersionId",
                table: "Games",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoUpdate",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "InstalledVersionId",
                table: "Games");
        }
    }
}
