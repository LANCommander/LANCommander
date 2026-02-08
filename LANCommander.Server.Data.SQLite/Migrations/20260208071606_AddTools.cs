using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ToolId",
                table: "Scripts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ToolId",
                table: "Pages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ToolId",
                table: "Archive",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tools_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tools_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GameTool",
                columns: table => new
                {
                    GamesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolsId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTool", x => new { x.GamesId, x.ToolsId });
                    table.ForeignKey(
                        name: "FK_GameTool_Games_GamesId",
                        column: x => x.GamesId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameTool_Tools_ToolsId",
                        column: x => x.ToolsId,
                        principalTable: "Tools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Scripts_ToolId",
                table: "Scripts",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_ToolId",
                table: "Pages",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_Archive_ToolId",
                table: "Archive",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTool_ToolsId",
                table: "GameTool",
                column: "ToolsId");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_CreatedById",
                table: "Tools",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_UpdatedById",
                table: "Tools",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_Tools_ToolId",
                table: "Archive",
                column: "ToolId",
                principalTable: "Tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_Tools_ToolId",
                table: "Pages",
                column: "ToolId",
                principalTable: "Tools",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_Tools_ToolId",
                table: "Scripts",
                column: "ToolId",
                principalTable: "Tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Archive_Tools_ToolId",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_Pages_Tools_ToolId",
                table: "Pages");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_Tools_ToolId",
                table: "Scripts");

            migrationBuilder.DropTable(
                name: "GameTool");

            migrationBuilder.DropTable(
                name: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_Scripts_ToolId",
                table: "Scripts");

            migrationBuilder.DropIndex(
                name: "IX_Pages_ToolId",
                table: "Pages");

            migrationBuilder.DropIndex(
                name: "IX_Archive_ToolId",
                table: "Archive");

            migrationBuilder.DropColumn(
                name: "ToolId",
                table: "Scripts");

            migrationBuilder.DropColumn(
                name: "ToolId",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "ToolId",
                table: "Archive");
        }
    }
}
