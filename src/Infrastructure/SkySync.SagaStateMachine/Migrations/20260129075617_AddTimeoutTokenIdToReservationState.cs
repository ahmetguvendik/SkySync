using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.SagaStateMachine.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeoutTokenIdToReservationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TimeoutTokenId",
                table: "ReservationState",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeoutTokenId",
                table: "ReservationState");
        }
    }
}
