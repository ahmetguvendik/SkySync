using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Notification.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationUsers",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivesOperationalEmails = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationUsers", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationUsers_ReceivesOperationalEmails",
                table: "NotificationUsers",
                column: "ReceivesOperationalEmails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationUsers");
        }
    }
}
