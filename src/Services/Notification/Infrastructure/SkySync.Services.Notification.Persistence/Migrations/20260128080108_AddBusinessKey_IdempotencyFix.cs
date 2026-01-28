using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Notification.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessKey_IdempotencyFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessKey",
                table: "InboxMessages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_EventType_BusinessKey",
                table: "InboxMessages",
                columns: new[] { "EventType", "BusinessKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_EventType_BusinessKey",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "BusinessKey",
                table: "InboxMessages");
        }
    }
}
