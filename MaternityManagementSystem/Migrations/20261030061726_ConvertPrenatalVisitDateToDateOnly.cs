using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPrenatalVisitDateToDateOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "VisitDate",
                table: "PrenatalVisits",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01'");

            migrationBuilder.Sql(
                """
        UPDATE PrenatalVisits
        SET VisitDate = date(VisitDateUtc, '+8 hours');
        """);

            migrationBuilder.DropColumn(
                name: "VisitDateUtc",
                table: "PrenatalVisits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VisitDateUtc",
                table: "PrenatalVisits",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.Sql(
                """
        UPDATE PrenatalVisits
        SET VisitDateUtc = datetime(VisitDate, '-8 hours');
        """);

            migrationBuilder.DropColumn(
                name: "VisitDate",
                table: "PrenatalVisits");
        }
    }
}
