using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySync.Services.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addRolesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var adminRoleId = new Guid("44E54B9F-0B4A-4FB6-8AC2-08F3AD85D3F1");
            var userRoleId = new Guid("6BE1578A-92C4-4A2D-9203-13DCF124BCAF");
            var seedTime = new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "CreatedTime", "ModifiedTime", "IsDeleted" },
                values: new object[,]
                {
                    { adminRoleId, "Admin", seedTime, seedTime, false },
                    { userRoleId, "User", seedTime, seedTime, false }
                });

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: userRoleId);

            migrationBuilder.Sql($@"
                UPDATE ""Users""
                SET ""RoleId"" = CASE
                    WHEN ""Role"" = 'Admin' THEN '{adminRoleId}'::uuid
                    ELSE '{userRoleId}'::uuid
                END;
            ");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var adminRoleId = new Guid("44E54B9F-0B4A-4FB6-8AC2-08F3AD85D3F1");
            var userRoleId = new Guid("6BE1578A-92C4-4A2D-9203-13DCF124BCAF");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "User");

            migrationBuilder.Sql($@"
                UPDATE ""Users""
                SET ""Role"" = CASE
                    WHEN ""RoleId"" = '{adminRoleId}'::uuid THEN 'Admin'
                    WHEN ""RoleId"" = '{userRoleId}'::uuid THEN 'User'
                    ELSE 'User'
                END;
            ");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
