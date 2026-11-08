using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "PlaySessions",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "PlaySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Map = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    GameVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ServerAddress = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Settings = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    LastEventOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Matches_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Matches_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Matches_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PlayerIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 158, nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerIdentities_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerIdentities_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlayerIdentities_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlayerIdentities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Team = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LinkSource = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAtMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    LeftAtMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MatchReporters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Trust = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PluginId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    LastSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    GapCount = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchReporters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchReporters_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchReporters_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchReporters_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchReporters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Team = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    Payload = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchResults_MatchParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "MatchParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchResults_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchResults_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchResults_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MatchStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Team = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchStatistics_MatchParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "MatchParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchStatistics_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReporterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    EndMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    UploadedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StorageLocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchAttachments_MatchReporters_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "MatchReporters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchAttachments_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchAttachments_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchAttachments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchAttachments_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MatchEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReporterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    GameTimeMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    Tick = table.Column<long>(type: "INTEGER", nullable: true),
                    ActorKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    SubjectKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: true),
                    Payload = table.Column<string>(type: "TEXT", nullable: true),
                    ClientTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchEvents_MatchReporters_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "MatchReporters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchEvents_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_ServerId",
                table: "PlaySessions",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchAttachments_CreatedById",
                table: "MatchAttachments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatchAttachments_MatchId",
                table: "MatchAttachments",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchAttachments_ReporterId",
                table: "MatchAttachments",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchAttachments_StorageLocationId",
                table: "MatchAttachments",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchAttachments_UpdatedById",
                table: "MatchAttachments",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CreatedById",
                table: "Matches",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_GameId_ExternalId",
                table: "Matches",
                columns: new[] { "GameId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_GameId_StartedOn",
                table: "Matches",
                columns: new[] { "GameId", "StartedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ServerId",
                table: "Matches",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Status_LastEventOn",
                table: "Matches",
                columns: new[] { "Status", "LastEventOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_UpdatedById",
                table: "Matches",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchId_GameTimeMilliseconds",
                table: "MatchEvents",
                columns: new[] { "MatchId", "GameTimeMilliseconds" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchId_Type",
                table: "MatchEvents",
                columns: new[] { "MatchId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_ReporterId_Sequence",
                table: "MatchEvents",
                columns: new[] { "ReporterId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_CreatedById",
                table: "MatchParticipants",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_MatchId_PlayerKey",
                table: "MatchParticipants",
                columns: new[] { "MatchId", "PlayerKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_UpdatedById",
                table: "MatchParticipants",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_UserId",
                table: "MatchParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReporters_CreatedById",
                table: "MatchReporters",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReporters_MatchId",
                table: "MatchReporters",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReporters_UpdatedById",
                table: "MatchReporters",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReporters_UserId",
                table: "MatchReporters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_CreatedById",
                table: "MatchResults",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_MatchId",
                table: "MatchResults",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_ParticipantId",
                table: "MatchResults",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_UpdatedById",
                table: "MatchResults",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatistics_Key_MatchId",
                table: "MatchStatistics",
                columns: new[] { "Key", "MatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatistics_MatchId_Key",
                table: "MatchStatistics",
                columns: new[] { "MatchId", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatistics_ParticipantId",
                table: "MatchStatistics",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerIdentities_CreatedById",
                table: "PlayerIdentities",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerIdentities_GameId_Kind_Value",
                table: "PlayerIdentities",
                columns: new[] { "GameId", "Kind", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerIdentities_Kind_Value",
                table: "PlayerIdentities",
                columns: new[] { "Kind", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerIdentities_UpdatedById",
                table: "PlayerIdentities",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerIdentities_UserId",
                table: "PlayerIdentities",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Servers_ServerId",
                table: "PlaySessions",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Servers_ServerId",
                table: "PlaySessions");

            migrationBuilder.DropTable(
                name: "MatchAttachments");

            migrationBuilder.DropTable(
                name: "MatchEvents");

            migrationBuilder.DropTable(
                name: "MatchResults");

            migrationBuilder.DropTable(
                name: "MatchStatistics");

            migrationBuilder.DropTable(
                name: "PlayerIdentities");

            migrationBuilder.DropTable(
                name: "MatchReporters");

            migrationBuilder.DropTable(
                name: "MatchParticipants");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_PlaySessions_ServerId",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Alias",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "PlaySessions");
        }
    }
}
