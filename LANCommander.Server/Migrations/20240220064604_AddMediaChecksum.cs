using Force.Crc32;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore.Migrations;
using System.IO;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaChecksum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Crc32",
                table: "Media",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Crc32",
                table: "Media");
        }
    }
}
