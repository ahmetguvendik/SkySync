using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePaymentTransactionToBaseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "PaymentTransactions",
                newName: "CreatedTime");

            // UpdatedDate NULL ise CreatedTime ile doldur (ModifiedTime non-nullable)
            migrationBuilder.Sql(
                "UPDATE \"PaymentTransactions\" SET \"UpdatedDate\" = \"CreatedTime\" WHERE \"UpdatedDate\" IS NULL;");

            migrationBuilder.RenameColumn(
                name: "UpdatedDate",
                table: "PaymentTransactions",
                newName: "ModifiedTime");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PaymentTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PaymentTransactions");

            migrationBuilder.RenameColumn(
                name: "CreatedTime",
                table: "PaymentTransactions",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "ModifiedTime",
                table: "PaymentTransactions",
                newName: "UpdatedDate");
        }
    }
}
