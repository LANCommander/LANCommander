using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivePatchesAndDefaultArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultArchiveId",
                table: "Games",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "ArchivePatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FromArchiveId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ToArchiveId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ObjectKey = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StorageLocationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UncompressedSize = table.Column<long>(type: "bigint", nullable: false),
                    CompressedSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedOn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivePatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivePatches_Archive_FromArchiveId",
                        column: x => x.FromArchiveId,
                        principalTable: "Archive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivePatches_Archive_ToArchiveId",
                        column: x => x.ToArchiveId,
                        principalTable: "Archive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivePatches_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivePatches_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ArchivePatches_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Games_DefaultArchiveId",
                table: "Games",
                column: "DefaultArchiveId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePatches_CreatedById",
                table: "ArchivePatches",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePatches_FromArchiveId",
                table: "ArchivePatches",
                column: "FromArchiveId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePatches_StorageLocationId",
                table: "ArchivePatches",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePatches_ToArchiveId",
                table: "ArchivePatches",
                column: "ToArchiveId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePatches_UpdatedById",
                table: "ArchivePatches",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Archive_DefaultArchiveId",
                table: "Games",
                column: "DefaultArchiveId",
                principalTable: "Archive",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Archive_DefaultArchiveId",
                table: "Games");

            migrationBuilder.DropTable(
                name: "ArchivePatches");

            migrationBuilder.DropIndex(
                name: "IX_Games_DefaultArchiveId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DefaultArchiveId",
                table: "Games");
        }
    }
}
