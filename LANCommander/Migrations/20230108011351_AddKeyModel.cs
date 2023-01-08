using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    public partial class AddKeyModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ValidKeyRegex",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllocationMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimedByMacAddress = table.Column<string>(type: "TEXT", maxLength: 17, nullable: true),
                    ClaimedByIpv4Address = table.Column<string>(type: "TEXT", maxLength: 15, nullable: true),
                    ClaimedByComputerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ClaimedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Keys_AspNetUsers_ClaimedByUserId",
                        column: x => x.ClaimedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Keys_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Keys_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Keys_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Keys_ClaimedByUserId",
                table: "Keys",
                column: "ClaimedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_CreatedById",
                table: "Keys",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_GameId",
                table: "Keys",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_UpdatedById",
                table: "Keys",
                column: "UpdatedById");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Keys");

            migrationBuilder.DropColumn(
                name: "ValidKeyRegex",
                table: "Games");
        }
    }
}
