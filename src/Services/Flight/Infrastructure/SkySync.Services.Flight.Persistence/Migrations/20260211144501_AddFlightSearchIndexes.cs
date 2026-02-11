using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Flight.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Flights_Departure_Destination_DepartureTime",
                table: "Flights",
                columns: new[] { "Departure", "Destination", "DepartureTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_DepartureTime",
                table: "Flights",
                column: "DepartureTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Flights_Departure_Destination_DepartureTime",
                table: "Flights");

            migrationBuilder.DropIndex(
                name: "IX_Flights_DepartureTime",
                table: "Flights");
        }
    }
}
