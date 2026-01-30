using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.SagaStateMachine.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightInfoToReservationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Departure",
                table: "ReservationState",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "ReservationState",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FlightNumber",
                table: "ReservationState",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Departure",
                table: "ReservationState");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "ReservationState");

            migrationBuilder.DropColumn(
                name: "FlightNumber",
                table: "ReservationState");
        }
    }
}
