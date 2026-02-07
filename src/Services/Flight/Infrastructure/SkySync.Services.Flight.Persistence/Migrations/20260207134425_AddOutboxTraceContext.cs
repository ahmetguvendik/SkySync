using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Flight.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxTraceContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Traceparent",
                table: "OutboxMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tracestate",
                table: "OutboxMessages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Traceparent",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Tracestate",
                table: "OutboxMessages");
        }
    }
}
