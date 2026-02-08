using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddToolActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ToolId",
                table: "Actions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Actions_ToolId",
                table: "Actions",
                column: "ToolId");

            migrationBuilder.AddForeignKey(
                name: "FK_Actions_Tools_ToolId",
                table: "Actions",
                column: "ToolId",
                principalTable: "Tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actions_Tools_ToolId",
                table: "Actions");

            migrationBuilder.DropIndex(
                name: "IX_Actions_ToolId",
                table: "Actions");

            migrationBuilder.DropColumn(
                name: "ToolId",
                table: "Actions");
        }
    }
}
