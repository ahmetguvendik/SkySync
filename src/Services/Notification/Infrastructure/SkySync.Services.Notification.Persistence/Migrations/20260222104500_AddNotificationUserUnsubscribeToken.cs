using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Notification.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationUserUnsubscribeToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UnsubscribeToken",
                table: "NotificationUsers",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationUsers_UnsubscribeToken",
                table: "NotificationUsers",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationUsers_UnsubscribeToken",
                table: "NotificationUsers");

            migrationBuilder.DropColumn(
                name: "UnsubscribeToken",
                table: "NotificationUsers");
        }
    }
}
