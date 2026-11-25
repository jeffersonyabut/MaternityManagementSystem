using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    public partial class EnforceNormalizedUsername : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the old unique username index.
            migrationBuilder.DropIndex(
                name: "IX_StaffMembers_Username",
                table: "StaffMembers");

            // Make the backfilled normalized username mandatory.
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUsername",
                table: "StaffMembers",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);


        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffMembers_NormalizedUsername",
                table: "StaffMembers");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUsername",
                table: "StaffMembers",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_Username",
                table: "StaffMembers",
                column: "Username",
                unique: true);
        }
    }
}
