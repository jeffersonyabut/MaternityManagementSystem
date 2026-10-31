using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddLivingChildrenCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LivingChildrenCount",
                table: "Pregnancies",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LivingChildrenCount",
                table: "Pregnancies");
        }
    }
}
