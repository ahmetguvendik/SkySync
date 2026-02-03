using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Reservation.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "InboxMessages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Processed");

            migrationBuilder.CreateTable(
                name: "FlightSummaries",
                columns: table => new
                {
                    FlightId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlightNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Departure = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Destination = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightSummaries", x => x.FlightId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightSummaries");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "InboxMessages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Processed",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
