using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Tests.SamplePlugin.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plugin_Sample_Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedOn = table.Column<long>(type: "bigint", nullable: true),
                    Rating = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugin_Sample_Notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugin_Sample_NoteTag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugin_Sample_NoteTag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plugin_Sample_NoteTag_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Plugin_Sample_Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plugin_Sample_Notes_TitleKey",
                table: "Plugin_Sample_Notes",
                column: "TitleKey");

            migrationBuilder.CreateIndex(
                name: "IX_Plugin_Sample_NoteTag_NoteId",
                table: "Plugin_Sample_NoteTag",
                column: "NoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plugin_Sample_NoteTag");

            migrationBuilder.DropTable(
                name: "Plugin_Sample_Notes");
        }
    }
}
