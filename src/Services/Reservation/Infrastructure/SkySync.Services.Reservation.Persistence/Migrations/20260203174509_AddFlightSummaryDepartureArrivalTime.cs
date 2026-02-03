using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Reservation.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightSummaryDepartureArrivalTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalTime",
                table: "FlightSummaries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartureTime",
                table: "FlightSummaries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalTime",
                table: "FlightSummaries");

            migrationBuilder.DropColumn(
                name: "DepartureTime",
                table: "FlightSummaries");
        }
    }
}
