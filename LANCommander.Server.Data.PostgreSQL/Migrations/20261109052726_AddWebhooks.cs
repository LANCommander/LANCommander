using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LANCommander.Server.Data.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebhookSubscription",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ProtectedSecret = table.Column<string>(type: "text", nullable: false),
                    EventTypesJson = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscription", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDelivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    Error = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDelivery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookDelivery_WebhookSubscription_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "WebhookSubscription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDelivery_EventId_SubscriptionId",
                table: "WebhookDelivery",
                columns: new[] { "EventId", "SubscriptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDelivery_Status_NextAttemptOn",
                table: "WebhookDelivery",
                columns: new[] { "Status", "NextAttemptOn" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDelivery_SubscriptionId",
                table: "WebhookDelivery",
                column: "SubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookDelivery");

            migrationBuilder.DropTable(
                name: "WebhookSubscription");
        }
    }
}
