using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientSmsReminderConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasSmsReminderConsent",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SmsReminderConsentRecordedAtUtc",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmsReminderConsentRecordedByStaffId",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_SmsReminderConsentRecordedByStaffId",
                table: "Patients",
                column: "SmsReminderConsentRecordedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_StaffMembers_SmsReminderConsentRecordedByStaffId",
                table: "Patients",
                column: "SmsReminderConsentRecordedByStaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_StaffMembers_SmsReminderConsentRecordedByStaffId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_SmsReminderConsentRecordedByStaffId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HasSmsReminderConsent",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "SmsReminderConsentRecordedAtUtc",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "SmsReminderConsentRecordedByStaffId",
                table: "Patients");
        }
    }
}
