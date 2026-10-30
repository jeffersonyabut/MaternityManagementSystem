using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddVitalSigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VitalSigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrenatalVisitId = table.Column<int>(type: "INTEGER", nullable: false),
                    SystolicBloodPressure = table.Column<int>(type: "INTEGER", nullable: true),
                    DiastolicBloodPressure = table.Column<int>(type: "INTEGER", nullable: true),
                    HeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    RespiratoryRate = table.Column<int>(type: "INTEGER", nullable: true),
                    TemperatureCelsius = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    OxygenSaturation = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    WeightKg = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    FetalHeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalSigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VitalSigns_PrenatalVisits_PrenatalVisitId",
                        column: x => x.PrenatalVisitId,
                        principalTable: "PrenatalVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VitalSigns_PrenatalVisitId",
                table: "VitalSigns",
                column: "PrenatalVisitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VitalSigns");
        }
    }
}
