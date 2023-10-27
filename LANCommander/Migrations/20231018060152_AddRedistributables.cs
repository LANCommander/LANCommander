using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddRedistributables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "Scripts",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "RedistributableId",
                table: "Scripts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "Archive",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "RedistributableId",
                table: "Archive",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Redistributables",
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
                    table.PrimaryKey("PK_Redistributables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Redistributables_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Redistributables_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Scripts_RedistributableId",
                table: "Scripts",
                column: "RedistributableId");

            migrationBuilder.CreateIndex(
                name: "IX_Archive_RedistributableId",
                table: "Archive",
                column: "RedistributableId");

            migrationBuilder.CreateIndex(
                name: "IX_Redistributables_CreatedById",
                table: "Redistributables",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Redistributables_UpdatedById",
                table: "Redistributables",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Archive_Redistributables_RedistributableId",
                table: "Archive",
                column: "RedistributableId",
                principalTable: "Redistributables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Scripts_Redistributables_RedistributableId",
                table: "Scripts",
                column: "RedistributableId",
                principalTable: "Redistributables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Archive_Redistributables_RedistributableId",
                table: "Archive");

            migrationBuilder.DropForeignKey(
                name: "FK_Scripts_Redistributables_RedistributableId",
                table: "Scripts");

            migrationBuilder.DropTable(
                name: "Redistributables");

            migrationBuilder.DropIndex(
                name: "IX_Scripts_RedistributableId",
                table: "Scripts");

            migrationBuilder.DropIndex(
                name: "IX_Archive_RedistributableId",
                table: "Archive");

            migrationBuilder.DropColumn(
                name: "RedistributableId",
                table: "Scripts");

            migrationBuilder.DropColumn(
                name: "RedistributableId",
                table: "Archive");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "Scripts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "Archive",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
