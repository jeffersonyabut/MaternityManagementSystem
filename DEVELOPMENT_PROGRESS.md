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

The application applies EF Core migrations during startup. Startup includes a staged username-normalization migration process that applies the normalized-username column migration, backfills existing usernames, and then applies later migrations. The repository contains 17 migration classes and an EF Core model snapshot.

Tests use temporary SQLite databases created with EnsureCreatedAsync; this does not verify the production migration sequence.

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
