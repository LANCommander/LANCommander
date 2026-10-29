using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.PostgreSQL.Migrations
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
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EngineSettings",
                table: "Servers",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Servers that used the built-in Docker engine now use the Docker plugin's, which keeps the host
            // and container in the engine settings
            migrationBuilder.Sql(@"UPDATE ""Servers"" SET ""Engine"" = 3, ""EngineExtensionId"" = 'lancommander.docker', ""EngineSettings"" = json_build_object('hostId', ""DockerHostId"", 'containerId', ""ContainerId"")::text WHERE ""Engine"" = 1");

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
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DockerHostId",
                table: "Servers",
                type: "uuid",
                nullable: true);

            // Give the built-in Docker engine its servers back
            migrationBuilder.Sql(@"UPDATE ""Servers"" SET ""Engine"" = 1, ""DockerHostId"" = NULLIF(""EngineSettings""::json->>'hostId', '')::uuid, ""ContainerId"" = COALESCE(""EngineSettings""::json->>'containerId', '') WHERE ""Engine"" = 3 AND ""EngineExtensionId"" = 'lancommander.docker' AND ""EngineSettings"" LIKE '{%'");

            migrationBuilder.DropColumn(
                name: "EngineExtensionId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "EngineSettings",
                table: "Servers");
        }
    }
}
