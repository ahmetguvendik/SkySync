using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Notification.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class mig_15022026 : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
