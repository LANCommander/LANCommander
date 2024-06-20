using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaThumbnail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ThumbnailId",
                table: "Media",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_ThumbnailId",
                table: "Media",
                column: "ThumbnailId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_Media_ThumbnailId",
                table: "Media",
                column: "ThumbnailId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Media_Media_ThumbnailId",
                table: "Media");

            migrationBuilder.DropIndex(
                name: "IX_Media_ThumbnailId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ThumbnailId",
                table: "Media");
        }
    }
}
