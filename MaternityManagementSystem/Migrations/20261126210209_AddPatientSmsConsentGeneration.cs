using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientSmsConsentGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentSmsReminders_AppointmentId_ScheduleRevision_ReminderType",
                table: "AppointmentSmsReminders");

            migrationBuilder.AddColumn<int>(
                name: "SmsReminderConsentGeneration",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConsentGeneration",
                table: "AppointmentSmsReminders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSmsReminders_AppointmentId_ScheduleRevision_ConsentGeneration_ReminderType",
                table: "AppointmentSmsReminders",
                columns: new[] { "AppointmentId", "ScheduleRevision", "ConsentGeneration", "ReminderType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The prior unique key cannot represent retained reminder history
            // from multiple consent generations. Check before changing indexes
            // or dropping either generation column. Raising ABORT causes the
            // migration transaction to roll back without changing schema/data.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "__ConsentGenerationDowngradeGuard" (
                    "Value" INTEGER NOT NULL
                );

                CREATE TEMP TRIGGER "__ConsentGenerationDowngradeGuardTrigger"
                BEFORE INSERT ON "__ConsentGenerationDowngradeGuard"
                WHEN EXISTS (
                    SELECT 1
                    FROM "AppointmentSmsReminders"
                    GROUP BY "AppointmentId", "ScheduleRevision", "ReminderType"
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    SELECT RAISE(
                        ABORT,
                        'Cannot downgrade SMS consent generations: reminder rows collide under the previous unique key. Preserve the rows and retain this migration until the history can be handled safely.'
                    );
                END;

                INSERT INTO "__ConsentGenerationDowngradeGuard" ("Value")
                VALUES (1);

                DROP TRIGGER "__ConsentGenerationDowngradeGuardTrigger";
                DROP TABLE "__ConsentGenerationDowngradeGuard";
                """);

            migrationBuilder.DropIndex(
                name: "IX_AppointmentSmsReminders_AppointmentId_ScheduleRevision_ConsentGeneration_ReminderType",
                table: "AppointmentSmsReminders");

            migrationBuilder.DropColumn(
                name: "SmsReminderConsentGeneration",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ConsentGeneration",
                table: "AppointmentSmsReminders");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSmsReminders_AppointmentId_ScheduleRevision_ReminderType",
                table: "AppointmentSmsReminders",
                columns: new[] { "AppointmentId", "ScheduleRevision", "ReminderType" },
                unique: true);
        }
    }
}
