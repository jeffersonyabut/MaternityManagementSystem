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
- Reconciliation is hooked after appointment creation, edit/reschedule, status change, cancellation, archive, patient consent changes, and contact-number edits. A single startup reconciliation recovers pending work; there is no timer or background polling worker.
- Provider dispatch is implemented as a separate one-pass operation below and is not called by appointment workflows or startup polling.

Verification results:

- **Focused reminder scheduling/reconciliation tests:** 26 passed, 0 failed, and 0 skipped.
- **Full test suite:** 217 passed, 0 failed, and 0 skipped.
- **Solution build:** succeeded; 0 warnings and 0 errors.
- **Database and messaging:** tests used temporary SQLite databases; no production database was accessed or migrated, and no SMS or network request occurred.

## Completed appointment SMS reminder dispatch pipeline (Milestone 4B)

The application can now process due reminder candidates through an explicit one-pass dispatch operation:

- `AppointmentSmsReminderRepository.TryClaimPendingReminderAsync` conditionally changes an eligible due `Pending` row to `Sending` with its attempt timestamp using one atomic SQLite update. Separate contexts cannot both claim the same row. The statement completes before the provider request begins.
- **Accepted dispatch authorization boundary:** the final eligibility validation immediately before dispatch authorizes the reminder request. It checks appointment status, archive state, future schedule and revision, explicit SMS consent and consent generation, contact number, and both global SMS settings. If any checked condition has changed before this validation, the reminder is suppressed and its status is finalized using the existing `Skipped` or `Superseded` status conventions.
- Consent withdrawal and appointment detail changes continue to reconcile pending reminders through the existing workflow hooks. The atomic database claim is short-lived; external UniSMS HTTP requests run outside database transactions.
- The dedicated message builder uses the clinic name, appointment date, and appointment time only. It produces a generic reminder and excludes patient identity and clinical details.
- The service uses the Milestone 4A `IUniSmsClient` reminder-send contract. Provider acceptance becomes `ProviderAccepted`; explicit rejection or credential unavailability becomes `Failed`; ambiguous transport, response, or cancellation outcomes become `Unknown`. Provider references/statuses and sanitized outcome codes are persisted.
- Unknown and stale Sending rows are terminal for automatic processing; dispatch only queries Pending rows and performs no automatic retry. No new status or schema change was needed.
- This is not a background worker. No polling loop or application startup dispatch was added, and appointment create/edit/cancel workflows do not wait for UniSMS.
- **Known limitation:** eligibility validation and the external UniSMS request are not atomic. Consent, appointment details, or SMS settings can change after final validation but before UniSMS receives the request. An in-flight or provider-accepted SMS cannot reliably be recalled. The system therefore does not guarantee cancellation of an SMS after dispatch authorization has begun.

Verification results:

- **Focused reminder scheduling/dispatch and UniSMS tests:** 70 passed, 0 failed, and 0 skipped.
- **Full test suite:** 247 passed, 0 failed, and 0 skipped.
- **Solution build:** succeeded; 0 warnings and 0 errors.
- **Dispatch tests:** used fake HTTP handlers and temporary SQLite databases, including separate-context concurrent claims and SQLite-triggered last-second eligibility changes.
- **Production database, migrations, and messaging:** no production database was accessed, no migration was applied, and no real SMS was sent.

## Completed appointment SMS background reminder worker (Milestone 4C)

Appointment reminder dispatch now runs in a cancellable polling worker owned by the WPF application. Automatic appointment SMS reminders are processed only while the Golddie WPF application is running; there is no independent Windows Service or always-on background process.

- Startup reconciliation completes before the worker is allowed to run. The worker starts after the main window is shown and polls immediately, then waits one minute between sequential cycles. Cycles cannot overlap; a failed reminder does not prevent other candidates from being considered, and unexpected cycle errors are contained so later polling continues.
- WPF application exit cancels the worker and waits up to five seconds for termination. A provider request already in progress receives cancellation; the shutdown wait remains bounded.
- The UniSMS client signals the request-start boundary immediately before `HttpClient.SendAsync`. Cancellation before that boundary conditionally releases a claimed `Sending` reminder to `Pending`. Once the boundary is crossed, cancellation is persisted as `Unknown` using a non-cancelled token. If outcome persistence fails, the row remains `Sending`; neither `Unknown` nor stale `Sending` is automatically retried.
- The existing atomic claim, last-second eligibility checks, both SMS settings, consent-generation and schedule-revision checks, generic message content, and provider-accepted status semantics remain in force. A provider outage is isolated from appointment and patient workflows.
- `IUniSmsCredentialProvider` allows HTTP tests to use fake credentials without invoking Windows DPAPI. Production continues to use `UniSmsCredentialService` and its existing DPAPI-protected credential file.
- Tests use temporary SQLite databases and fake HTTP handlers. No production database was accessed, no migration was run, and no real SMS was sent.

Verification results:

- **Focused worker, dispatch, and UniSMS client tests:** 49 passed, 0 failed, and 0 skipped.
- **Reminder and UniSMS tests:** 87 passed, 0 failed, and 0 skipped under the loaded host Windows user profile.
- **UniSmsCredentialServiceTests:** 5 passed, 0 failed, and 0 skipped. The three DPAPI credential-storage tests that previously failed passed under the loaded host Windows user profile. Those earlier failures were caused by the sandbox identity lacking a loaded Windows user profile; no application regression was confirmed.
- **Full solution tests:** 257 passed, 0 failed, and 0 skipped under the loaded host Windows user profile.
- **Solution build:** succeeded with 0 errors and 0 warnings. The test build emitted 10 existing CS0108 warnings (five report-view `BorderBrush` warnings emitted for both WPF targets).
- **Diff check:** `git diff --check` passed; Git printed only line-ending normalization notices.
- **Limitations:** WPF shutdown can wait no longer than five seconds. If an operation outlives that bound, an unresolved attempt may remain `Sending`; ambiguous `Sending` and `Unknown` attempts are not automatically retried. Eligibility and provider request initiation still cannot be made atomic with changes by another staff action.

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

## Completed SMS reminder monitoring data layer (Milestone 4D.1)

Added a read-only monitoring projection and repository queries for appointment SMS reminders. The flat result contains
only the patient first and last name, patient number, appointment date/time snapshots, reminder type and status,
sanitized outcome code, provider status, and reminder lifecycle timestamps. It excludes patient contact/address data,
clinical details, message content, provider references, credentials, raw provider responses, and EF entities.

- Monitoring queries support status, reminder type, inclusive appointment-date range, and patient name/number filters,
  plus total count and bounded pagination. Queries use EF Core `AsNoTracking()` and database projections.
- Queue work (`Pending` and `Sending`) sorts by scheduled time; historical records sort by status-change time newest
  first. Reminder ID provides deterministic tie-breaking. Grouped counts always include all seven statuses, including
  zero-count statuses.
- Appointment date/time snapshots remain clinic-local. After SQLite materialization, lifecycle timestamps have their
  UTC `DateTimeKind` restored with a value-preserving operation; no timezone conversion occurs in the repository.
  Null lifecycle timestamps remain null, and regional formatting/conversion stays at the presentation boundary.
- No schema, scheduling, reconciliation, claiming, dispatch, worker, or provider lookup behavior changed. No production
  database was accessed and no real SMS was sent. Existing migration tests in the full suite used temporary SQLite
  databases only; no production migration was run.

Verification results:

- **Focused monitoring repository tests:** 12 passed, 0 failed, and 0 skipped, including UTC kind/value preservation,
  nullable timestamp, and clinic-local snapshot checks.
- **Existing reminder and UniSMS tests:** 99 passed, 0 failed, and 0 skipped under the loaded host Windows profile.
- **Full solution tests:** 269 passed, 0 failed, and 0 skipped under the loaded host Windows profile. The sandbox
  profile still cannot run the three DPAPI credential-storage tests because it has no loaded Windows user profile.
- **Solution build:** succeeded with 0 warnings and 0 errors.
- **Diff check:** `git diff --check` passed.

Remaining Milestone 4D work includes the Admin-only monitoring page and navigation, followed by any separately approved
provider delivery-status lookup and operational workflow for ambiguous outcomes. Provider acceptance continues to mean
acceptance by the provider, not confirmed delivery.

## Completed Admin SMS reminder monitoring service (Milestone 4D.2.1)

Added `IAppointmentSmsReminderMonitoringService` and `AppointmentSmsReminderMonitoringService` as the application
service boundary for read-only reminder monitoring:

- The service accepts `AppointmentSmsReminderRepository` and `AuthorizationService`. Both public data-returning
  operations call `AuthorizationService.EnsureAdminAsync` before accessing the repository. Authorization failures
  remain `UnauthorizedAccessException`.
- `GetMonitoringPageAsync` forwards the existing query and cancellation token and returns the existing paged monitoring
  DTO. `GetMonitoringStatusCountsAsync` forwards its cancellation token and returns the existing all-seven-status count
  DTOs. Repository filtering, projection, pagination, sorting, and timestamp behavior are unchanged.
- No ViewModel, WPF page, navigation, provider lookup, scheduling/dispatch/worker behavior, schema, or migration changed.
  Tests use temporary SQLite databases only; no production database was accessed and no SMS was sent.

Verification results:

- **Focused monitoring tests:** 15 passed, 0 failed, and 0 skipped (12 repository tests and 3 service tests).
- **Solution build:** succeeded with 0 errors and 10 existing CS0108 report-view `BorderBrush` warnings.

### Monitoring service authorization test follow-up

Strengthened the service tests to exercise both public operations independently with no session, a deactivated Admin,
an Admin whose database role changed, and an Admin whose security stamp changed. Added a throwing context-factory check
proving unauthorized calls do not reach the monitoring repository, and asserted that both cancellation exceptions
carry the exact caller token. No production service or authorization logic changed.

Verification results for this test-only follow-up:

- **Focused monitoring and authorization tests:** 30 passed, 0 failed, and 0 skipped.
- **Full solution tests:** 278 passed, 3 failed, and 0 skipped. The three failures were existing
  `UniSmsCredentialServiceTests` cases that threw `IOException` while securely storing the API key; no monitoring or
  authorization tests failed.
- **Solution build:** succeeded with 0 warnings and 0 errors.
- **Diff check:** `git diff --check` passed.

Remaining 4D.2 work is the monitoring ViewModel, followed by the Admin WPF page and navigation. Provider delivery-status
lookup and operational handling for ambiguous outcomes remain separate future work.

## Completed SMS reminder monitoring ViewModel (Milestone 4D.2.2)

Added `AppointmentSmsReminderMonitoringViewModel` using the existing monitoring service and DTO contracts:

- Exposes observable reminder records, all seven status counts, search/status/type/date filters, page state, busy state,
  and a user-facing error message. It does not transform appointment-local snapshots or UTC lifecycle timestamps.
- Supports load, refresh, apply/clear filters, and bounded previous/next page commands. Search and other editable filters
  are submitted only when applied; refresh and paging retain the last applied filter set. Invalid date ranges do not
  call the service, and overlapping operations are ignored while busy.
- Added deterministic fake-service tests for initial load, refresh, filter application/reset, pagination boundaries,
  explicit search application, invalid ranges, service errors, busy-state cleanup, and overlapping requests.
- No dashboard wiring, navigation, XAML, database, migration, SMS provider, scheduling, dispatch, or worker behavior
  changed.

Verification results:

- **Focused monitoring ViewModel, service, and repository tests:** 34 passed, 0 failed, and 0 skipped. The test build
  emitted 10 existing CS0108 report-view `BorderBrush` warnings (five for each WPF target).
- **Solution build:** succeeded with 0 warnings and 0 errors.
- **Diff check:** `git diff --check` passed.

## Completed Admin dashboard SMS reminder monitoring page (Milestone 4D.2.3)

Added the SMS Reminder Monitoring item to the Admin dashboard and routed it through the existing content host:

- The navigation item follows the existing sidebar style and Admin visibility rule. The navigation method also checks
  the current session role before constructing the page. The XAML owns the single Click hookup.
- Dashboard composition creates the existing `AuthorizationService`, monitoring repository, monitoring service, and
  `AppointmentSmsReminderMonitoringViewModel` manually. The new view receives that ViewModel and starts its existing
  `LoadCommand` when loaded. `NavigationService` and the monitoring service authorization boundary are unchanged.
- The initial page displays all seven status counts, patient first/last name and patient number, appointment-local
  date/time, reminder type/status, and lifecycle timestamps formatted through `RegionalSettingsService`. It includes
  search and status/type/date filters, apply/clear/refresh actions, pagination, and loading/error states. The lifecycle
  formatter converts for display only; the stored UTC values are untouched. ProviderAccepted is described as provider
  acceptance, not confirmed recipient delivery. The view exposes no phone, message body, provider reference/response,
  credentials, or clinical data.
- No reminder scheduling, dispatch, worker, UniSMS client, schema, or migration behavior changed. No production
  database was accessed and no SMS was sent.

Verification results:

- **Focused monitoring ViewModel, service, and repository tests:** 34 passed, 0 failed, and 0 skipped.
- **Solution build:** succeeded with 0 errors and 10 existing CS0108 report-view `BorderBrush` warnings.
- **Diff check:** `git diff --check` passed.

The next Milestone 4D work is review and visual verification of the monitoring page. Provider delivery-status lookup and
operational handling for ambiguous outcomes remain separate future work.

## Completed UI theme foundation (UI Redesign Stage 1)

Added application-wide `Light`, `Dark`, and `System` theme preferences while preserving the existing MaterialDesign 3
defaults and application shell:

- Added matching Light and Dark resource dictionaries with semantic colors for shell surfaces, content, text, borders,
  navigation, inputs, disabled text, validation, selection, and accent states. The active palette is replaced in the
  application merged dictionaries, so existing and future `DynamicResource` consumers update immediately.
- Added a theme service that applies explicit preferences or follows the Windows app theme. System mode reads the
  current-user `AppsUseLightTheme` value, falls back to `SystemUsesLightTheme`, and listens for Windows user preference
  change notifications. If Windows theme settings cannot be read, it falls back to Light.
- Theme preference files are stored separately as
  `%LocalAppData%\Golddie\Preferences\theme-{staffId}.json`. The store uses same-directory temporary files and
  atomic replacement, contains only a theme name, and falls back to System for missing, malformed, or inaccessible
  files. It does not use clinic SQLite or `SystemSettingRepository`.
- After the existing login flow starts the session, `MainWindow` loads that session's staff ID preference before showing
  the dashboard. A small Light/Dark/System selector in the existing dashboard top bar applies changes immediately and
  saves them for the current staff account. Authentication, authorization, navigation, and session creation logic are
  otherwise unchanged.
- Migrated the shell and primary dashboard surfaces/text/selection states to semantic `DynamicResource` keys for
  visual theme validation. Dashboard content, layout, navigation, bindings, commands, status meanings, report styling,
  and other views remain unchanged.
- No database schema, migration, production SQLite access, or SMS behavior changed.

Verification results:

- **Theme and authorization tests:** 15 passed, 0 failed, and 0 skipped (9 theme tests and 6 existing authorization
  tests). The test build emitted 10 existing CS0108 report-view `BorderBrush` warnings.
- **Solution build:** succeeded with 0 errors and 0 warnings on the final build.
- **Diff check:** `git diff --check` passed.
- **Manual WPF verification:** Light/Dark theme switching works in the running application without restarting.
  Per-account preference restoration and live Windows System-theme detection were not manually verified.

Remaining redesign work is the incremental migration of other workflow views and dialogs to shared semantic resources.
Report/print styling remains separate from the application theme unless a later stage explicitly changes that scope.

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
- Review manual handling of `Unknown` SMS outcomes; automatic retries remain intentionally disabled.
