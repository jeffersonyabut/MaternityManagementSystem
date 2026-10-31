using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPregnancyObstetricDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Complications",
                table: "Pregnancies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMethod",
                table: "Pregnancies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GestationalAgeAtEndDays",
                table: "Pregnancies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GestationalAgeAtEndWeeks",
                table: "Pregnancies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfInfants",
                table: "Pregnancies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PregnancyOutcome",
                table: "Pregnancies",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Complications",
                table: "Pregnancies");

            migrationBuilder.DropColumn(
                name: "DeliveryMethod",
                table: "Pregnancies");

            migrationBuilder.DropColumn(
                name: "GestationalAgeAtEndDays",
                table: "Pregnancies");

            migrationBuilder.DropColumn(
                name: "GestationalAgeAtEndWeeks",
                table: "Pregnancies");

            migrationBuilder.DropColumn(
                name: "NumberOfInfants",
                table: "Pregnancies");

            migrationBuilder.DropColumn(
                name: "PregnancyOutcome",
                table: "Pregnancies");
        }
    }
}
