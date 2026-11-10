
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "StaffMembers",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            // Generate a separate random 32-character stamp for every existing account.
            migrationBuilder.Sql(
                """
                UPDATE StaffMembers
                SET SecurityStamp = lower(hex(randomblob(16)))
                WHERE SecurityStamp IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                table: "StaffMembers",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 32,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "StaffMembers");
        }
    }
}
