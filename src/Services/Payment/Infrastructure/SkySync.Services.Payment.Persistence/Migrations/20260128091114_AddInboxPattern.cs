using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxPattern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventPayload = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Processed"),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_EventType",
                table: "InboxMessages",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_EventType_BusinessKey",
                table: "InboxMessages",
                columns: new[] { "EventType", "BusinessKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_EventType_ProcessedAt",
                table: "InboxMessages",
                columns: new[] { "EventType", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAt",
                table: "InboxMessages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status",
                table: "InboxMessages",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");
        }
    }
}
