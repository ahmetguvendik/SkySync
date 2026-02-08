using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTimedOutReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimedOutReservations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimedOutReservations",
                columns: table => new
                {
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimedOutReservations", x => x.ReservationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimedOutReservations_TimedOutAt",
                table: "TimedOutReservations",
                column: "TimedOutAt");
        }
    }
}
