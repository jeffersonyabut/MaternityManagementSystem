using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPregnancies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pregnancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    LastMenstrualPeriod = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EstimatedDueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PregnancyEndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pregnancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pregnancies_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pregnancies_PatientId",
                table: "Pregnancies",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pregnancies");
        }
    }
}
