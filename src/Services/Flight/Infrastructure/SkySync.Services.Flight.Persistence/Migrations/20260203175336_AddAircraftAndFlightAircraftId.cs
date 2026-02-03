using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SkySync.Services.Flight.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAircraftAndFlightAircraftId : Migration
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
                name: "Aircraft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SeatCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aircraft", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Aircraft",
                columns: new[] { "Id", "Name", "SeatCount" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), "Boeing 737-800", 180 },
                    { new Guid("11111111-1111-1111-1111-111111111102"), "Airbus A320", 150 },
                    { new Guid("11111111-1111-1111-1111-111111111103"), "Boeing 777-300", 250 },
                    { new Guid("11111111-1111-1111-1111-111111111104"), "Embraer E190", 100 },
                    { new Guid("11111111-1111-1111-1111-111111111105"), "ATR 72", 72 }
                });

            // Mevcut uçuşlar için varsayılan: ilk demo uçak (Boeing 737-800)
            var defaultAircraftId = new Guid("11111111-1111-1111-1111-111111111101");
            migrationBuilder.AddColumn<Guid>(
                name: "AircraftId",
                table: "Flights",
                type: "uuid",
                nullable: false,
                defaultValue: defaultAircraftId);

            migrationBuilder.CreateIndex(
                name: "IX_Flights_AircraftId",
                table: "Flights",
                column: "AircraftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_Aircraft_AircraftId",
                table: "Flights",
                column: "AircraftId",
                principalTable: "Aircraft",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flights_Aircraft_AircraftId",
                table: "Flights");

            migrationBuilder.DropTable(
                name: "Aircraft");

            migrationBuilder.DropIndex(
                name: "IX_Flights_AircraftId",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "AircraftId",
                table: "Flights");

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
