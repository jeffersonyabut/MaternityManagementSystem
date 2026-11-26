using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentSmsReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduleRevision",
                table: "Appointments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AppointmentSmsReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppointmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduleRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    ReminderType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppointmentDateSnapshot = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AppointmentStartTimeSnapshot = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StatusChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AttemptStartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinalizedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProviderReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ProviderStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SanitizedOutcomeCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSmsReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentSmsReminders_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSmsReminders_AppointmentId_ScheduleRevision_ReminderType",
                table: "AppointmentSmsReminders",
                columns: new[] { "AppointmentId", "ScheduleRevision", "ReminderType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSmsReminders_ProviderReference",
                table: "AppointmentSmsReminders",
                column: "ProviderReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSmsReminders_Status_ScheduledAtUtc",
                table: "AppointmentSmsReminders",
                columns: new[] { "Status", "ScheduledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentSmsReminders");

            migrationBuilder.DropColumn(
                name: "ScheduleRevision",
                table: "Appointments");
        }
    }
}
