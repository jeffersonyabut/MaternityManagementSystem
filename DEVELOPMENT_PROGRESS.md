# Golddie Development Progress

## Project and architecture

Golddie is a maternity clinic management system built with C#, WPF, .NET 10 for Windows, EF Core, SQLite, CommunityToolkit.Mvvm, MaterialDesignInXamlToolkit, and LiveCharts.

The solution contains one WPF application and one xUnit test project. The application is organized around WPF views and view models, services, repositories, EF Core data access, models, and supporting navigation and session code.

Established architecture decisions:

- Keep one WPF application; do not split it into separate Admin and Nurse applications.
- Keep SQLite as the database.
- Use shared services and a shared data layer.
- Provide role-specific Admin and Nurse/Midwife experiences within the application.
- Prioritize Nurse-side workflows.
- Keep manual vital-sign entry supported alongside future ESP32 device integration.

## Database and migrations

ClinicDbContext uses SQLite. Its default database is clinic.db under the current user's Local Application Data Golddie directory.

The application applies EF Core migrations during startup. Startup includes a staged username-normalization migration process that applies the normalized-username column migration, backfills existing usernames, and then applies later migrations. The repository contains 21 migration classes and an EF Core model snapshot.

Most tests use temporary SQLite databases created with EnsureCreatedAsync. Focused migration tests also exercise migration Up and Down against temporary SQLite databases; no production migration is applied by those tests.

The assumption behind the existing +8-hour prenatal date migration has not been confirmed and remains a pending review item.

## Completed development improvements

Steps 1 through 5 represent completed development improvements; they do not necessarily correspond to five separate commits.

1. **Patient update field assignments:** PatientRepository.UpdateAsync now explicitly assigns editable fields, preserving PatientNumber and CreatedAtUtc.
2. **Password policy:** Password validation uses a standardized minimum length of 12 characters.
3. **Pregnancy validation and obstetric history:** Pregnancy input validation and obstetric-history calculations were updated.
4. **Vital-sign validation and archive handling:** VitalSignValidator and VitalSignService include the confirmed validation and archive-handling changes.
5. **Patient search consistency:** Relevant patient search methods share consistent search behavior.

Clinical policies or workflow decisions implied by these changes that have not been approved by the clinic remain subject to confirmation.

## Completed laboratory workflow

The laboratory-result workflow is implemented across the data layer and user interface:

- Added the laboratory lifecycle data model, EF Core migration, validation, service, repository, and audit tracking.
- Added laboratory registration and editing workflows.
- Added laboratory history to the patient profile.
- Added laboratory status, requested date, test date, and result availability display to prenatal visits.

Verification results:

- **Focused laboratory workflow tests:** 18 passed, 0 failed.
- **Full test suite:** 141 passed, 0 failed, and 0 skipped.
- **Solution build:** 0 errors and 10 existing CS0108 warnings.
- **Manual verification:** Patient profile laboratory history and prenatal visit laboratory status displays were verified.

The migration is present in the application, but the production SQLite database migration has not been applied as part of this work.

## Completed administrative database backup

An Admin-only live SQLite database backup action is available from System Settings:

- `DatabaseBackupService` uses `SqliteConnection.BackupDatabase` to create a consistent backup without copying the live database file directly.
- The service writes to a timestamped temporary file in the selected destination and validates it with `PRAGMA integrity_check` and `PRAGMA foreign_key_check` before moving it to its final backup filename.
- Service-layer access is restricted with `AuthorizationService.EnsureAdminAsync`; the settings UI does not serve as the only authorization boundary.
- Backup failures are reported without exposing database contents or low-level exception details.

Verification results:

- **Focused database backup tests:** 3 passed, 0 failed.
- **Solution build:** succeeded; 0 errors and 0 warnings.
- **Manual verification:** Admin backup workflow was verified in the WPF application.
- **Database restoration:** not implemented or verified.
- **Production migration:** no production SQLite database migration was run for this feature.

## Completed UniSMS test messaging integration

UniSMS API key configuration and an Admin-only manual test SMS workflow are available in System Settings:

- `UniSmsCredentialService` stores the API Secret Key encrypted with Windows DPAPI using the `CurrentUser` scope under the local Golddie application data directory. Credential access is protected by `AuthorizationService.EnsureAdminAsync`.
- `UniSmsClient` sends the fixed test message to `POST https://unismsapi.com/api/sms` using HTTP Basic Authentication (API key as username and an empty password), with the `Unisoft` sender ID.
- The Admin explicitly enters a Philippine mobile number and selects **Send Test SMS**. The interface prevents repeat submission while a request is in progress. No automatic appointment reminders or retries are implemented.
- Provider message statuses are presented distinctly. Pending and retrying are not described as delivered; provider-reported sent is not described as confirmed receipt. Unclear outcomes direct the Admin to check the provider before trying again.
- Client tests use a fake HTTP handler and temporary test data; they make no real provider requests.

Verification results:

- **Focused UniSMS tests:** 14 passed, 0 failed, and 0 skipped.
- **Full test suite:** 173 passed, 0 failed, and 0 skipped.
- **Solution build:** succeeded; 0 errors and 0 warnings.
- **Manual verification:** a test SMS was successfully received on a TM mobile number.
- **Production database:** no production SQLite database was accessed or modified, and no migration was run for this feature.

## Completed appointment SMS consent-generation schema

The patient SMS consent and appointment reminder schema now supports separate consent generations without changing the meaning of `Appointment.ScheduleRevision`:

- Added `Patient.SmsReminderConsentGeneration` and `AppointmentSmsReminder.ConsentGeneration`, both defaulting to `0`; existing rows are migrated with value `0`. A new patient who explicitly grants consent during registration starts at generation `1`.
- Expanded the reminder unique key to `(AppointmentId, ScheduleRevision, ConsentGeneration, ReminderType)`, allowing a new reminder pair after consent is granted again while retaining terminal history from previous consent decisions.
- Explicit consent state changes increment the generation, including a grant selected during registration. Repeating the current consent state and ordinary patient profile edits do not. Existing consent timestamps, staff attribution, audit logs, and record versions remain recorded.
- Migration `20261126210209_AddPatientSmsConsentGeneration` updates the schema and snapshot. Its Down migration checks for collisions under the old unique key before changing indexes or dropping columns. A collision aborts with an actionable error; the migration transaction leaves schema and reminder rows unchanged.
- This schema checkpoint did not implement scheduling or reconciliation; those capabilities are recorded in the following section. Workers and automatic SMS dispatch remain unimplemented.

Verification results:

- **Focused patient consent, consent-generation, and reminder schema tests:** 18 passed, 0 failed, and 0 skipped.
- **Full test suite:** 191 passed, 0 failed, and 0 skipped.
- **Solution build:** succeeded; 0 warnings and 0 errors.
- **Migration verification:** Up and collision-free Down preserved test data; collision Down failed safely and retained the expanded schema and all reminder rows. Tests used temporary SQLite databases only.
- **Production database and messaging:** no production database was accessed or migrated; no SMS was sent and no network request was made.

## Completed appointment SMS reminder scheduling and reconciliation

Persistent appointment reminders are scheduled and reconciled locally without sending SMS:

- `AppointmentSmsReminderService` converts clinic-local appointment date/time using `Singapore Standard Time` (Asia/Manila, UTC+8), calculates the exact 72-hour and 24-hour instants, and stores them as UTC. SQLite `DateTimeKind.Unspecified` values from UTC fields are explicitly treated as UTC.
- Eligible future Scheduled and Confirmed appointments with current patient consent and a usable Philippine mobile number receive pending reminders for their current schedule revision and consent generation. No recipient number, appointment purpose, pregnancy detail, or message content is stored in reminder rows.
- Reconciliation is idempotent. A process-wide gate serializes overlapping passes, while the database unique key and duplicate-safe repository insertion protect reminder creation. Old schedule revisions are superseded; pending reminders are skipped when an appointment has passed, is archived/ineligible, lacks consent, or lacks a usable contact. Terminal and Sending rows are not reset.
- If both reminders are overdue while settings are enabled, the advance reminder is superseded and the 24-hour reminder remains pending. A lone overdue advance reminder remains pending. Disabled global SMS settings retain pending work and suppress due candidates; a later reconciliation after re-enabling applies catch-up rules.
- Reconciliation is hooked after appointment creation, edit/reschedule, status change, cancellation, archive, patient consent changes, and contact-number edits. A single startup reconciliation recovers pending work; there is no timer or background worker in this milestone.
- Provider dispatch is separate and remains unimplemented. No UniSMS calls, network requests, retries, or automatic SMS sends are made.

Verification results:

- **Focused reminder scheduling/reconciliation tests:** 26 passed, 0 failed, and 0 skipped.
- **Full test suite:** 217 passed, 0 failed, and 0 skipped.
- **Solution build:** succeeded; 0 warnings and 0 errors.
- **Database and messaging:** tests used temporary SQLite databases; no production database was accessed or migrated, and no SMS or network request occurred.

## Baseline verification

Baseline commands were run against the solution:

    dotnet build D:\Thesis\MaternityManagementSystem\MaternityManagementSystem.slnx
    dotnet test D:\Thesis\MaternityManagementSystem\MaternityManagementSystem.slnx

Results:

- **Build:** succeeded; 0 errors and 10 warnings.
- **Warnings:** five distinct CS0108 warnings about BorderBrush hiding Control.BorderBrush, each emitted for both the temporary WPF project and the main project. They occur in:
  - Views/Reports/AppointmentReportView.xaml.cs
  - Views/Reports/IndividualPatientRecordReportView.xaml.cs
  - Views/Reports/LaboratoryResultsReportView.xaml.cs
  - Views/Reports/PregnancySummaryReportView.xaml.cs
  - Views/Reports/PrenatalCareReportView.xaml.cs
- **Tests:** 123 passed, 0 failed, and 0 skipped.

No migrations or database updates were run during baseline verification.

## Pre-existing working-tree changes

At inspection, git status reported the following 13 paths as modified. These were pre-existing working-tree entries and are not changes from the documentation work:

- MaternityManagementSystem/DashboardWindow.xaml
- MaternityManagementSystem/Dialogs/StaffRegistrationDialog.xaml.cs
- MaternityManagementSystem/MainWindow.xaml
- MaternityManagementSystem/MaternityManagementSystem.csproj
- MaternityManagementSystem/Repositories/PatientRepository.cs
- MaternityManagementSystem/Services/ObstetricHistoryService.cs
- MaternityManagementSystem/Services/PregnancyValidator.cs
- MaternityManagementSystem/Services/StaffManagementService.cs
- MaternityManagementSystem/Services/VitalSignService.cs
- MaternityManagementSystem/Services/VitalSignValidator.cs
- MaternityManagementSystem/Views/DashboardView.xaml
- MaternityManagementSystem/Views/PatientListView.xaml
- MaternityManagementSystem/Views/PatientProfileView.xaml

Four paths had identical worktree and HEAD hashes during inspection despite being reported as modified by Git. This discrepancy is unexplained; it was not investigated or changed. No staged or untracked files were reported.

## Remaining roadmap

All items below are **pending**. Clinical policies and clinic workflow decisions remain subject to confirmation.

- Standardize archive-reason validation.
- Review concurrency handling for last-admin protection.
- Validate UserSession constructor arguments.
- Confirm the +8-hour prenatal date migration assumption.
- Decide whether archive operations should create RecordVersion entries.
- Review the StaffId foreign-key policy for audit logs and record versions.
- Improve exception handling and internal logging.
- Review role canonicalization and repository role invariants.
