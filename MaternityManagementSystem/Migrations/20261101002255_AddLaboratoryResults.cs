using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddLaboratoryResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LaboratoryResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    PrenatalVisitId = table.Column<int>(type: "INTEGER", nullable: true),
                    TestName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    TestDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ReferenceRange = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchiveReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaboratoryResults_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaboratoryResults_PrenatalVisits_PrenatalVisitId",
                        column: x => x.PrenatalVisitId,
                        principalTable: "PrenatalVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryResults_PatientId",
                table: "LaboratoryResults",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryResults_PrenatalVisitId",
                table: "LaboratoryResults",
                column: "PrenatalVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryResults_TestDate",
                table: "LaboratoryResults",
                column: "TestDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaboratoryResults");
        }
    }
}
