using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddServerEngineExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EngineExtensionId",
                table: "Servers",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EngineSettings",
                table: "Servers",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Servers that used the built-in Docker engine now use the Docker plugin's, which keeps the host
            // and container in the engine settings
            migrationBuilder.Sql(@"UPDATE `Servers` SET `Engine` = 3, `EngineExtensionId` = 'lancommander.docker', `EngineSettings` = JSON_OBJECT('hostId', LOWER(`DockerHostId`), 'containerId', `ContainerId`) WHERE `Engine` = 1");

            migrationBuilder.DropColumn(
                name: "ContainerId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "DockerHostId",
                table: "Servers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContainerId",
                table: "Servers",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "DockerHostId",
                table: "Servers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            // Give the built-in Docker engine its servers back
            migrationBuilder.Sql(@"UPDATE `Servers` SET `Engine` = 1, `DockerHostId` = NULLIF(JSON_UNQUOTE(JSON_EXTRACT(`EngineSettings`, '$.hostId')), 'null'), `ContainerId` = COALESCE(JSON_UNQUOTE(JSON_EXTRACT(`EngineSettings`, '$.containerId')), '') WHERE `Engine` = 3 AND `EngineExtensionId` = 'lancommander.docker' AND JSON_VALID(`EngineSettings`)");

            migrationBuilder.DropColumn(
                name: "EngineExtensionId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "EngineSettings",
                table: "Servers");
        }
    }
}
