using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class AppointmentSmsReminderServiceTests
{
    private static readonly DateTime BeforeAdvanceDueUtc =
        new(2026, 12, 6, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime AdvanceDueUtc =
        new(2026, 12, 7, 1, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime FinalDueUtc =
        new(2026, 12, 9, 1, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime AppointmentAtUtc =
        new(2026, 12, 10, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CalculateSchedule_UsesManilaLocalTimeAndExactLeadTimes()
    {
        Assert.Equal(
            AppointmentSmsReminderService.ClinicTimeZoneId,
            TimeZoneInfo.FindSystemTimeZoneById(
                AppointmentSmsReminderService.ClinicTimeZoneId).Id);

        var schedule = AppointmentSmsReminderService.CalculateSchedule(
            new DateOnly(2026, 12, 10),
            new TimeOnly(9, 0));

        Assert.Equal(AppointmentAtUtc, schedule.AppointmentAtUtc);
        Assert.Equal(AdvanceDueUtc, schedule.Advance3DayAtUtc);
        Assert.Equal(FinalDueUtc, schedule.Final24HourAtUtc);
        Assert.Equal(TimeSpan.FromHours(72),
            schedule.AppointmentAtUtc - schedule.Advance3DayAtUtc);
        Assert.Equal(TimeSpan.FromHours(24),
            schedule.AppointmentAtUtc - schedule.Final24HourAtUtc);
    }

    [Fact]
    public void TreatAsUtc_UnspecifiedSqliteValues_DoesNotConvertUsingMachineTimezone()
    {
        var sqliteValue = new DateTime(2026, 12, 9, 1, 0, 0, DateTimeKind.Unspecified);

        DateTime result = AppointmentSmsReminderService.TreatAsUtc(sqliteValue);

        Assert.Equal(sqliteValue.Ticks, result.Ticks);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("Scheduled", false, true, "09123456789", true)]
    [InlineData("Confirmed", false, true, "+639123456789", true)]
    [InlineData("Completed", false, true, "09123456789", false)]
    [InlineData("Cancelled", false, true, "09123456789", false)]
    [InlineData("No-show", false, true, "09123456789", false)]
    [InlineData("Scheduled", true, true, "09123456789", false)]
    [InlineData("Scheduled", false, false, "09123456789", false)]
    [InlineData("Scheduled", false, true, "", false)]
    [InlineData("Scheduled", false, true, "not-a-mobile", false)]
    public async Task ReconcileAppointment_OnlySchedulesForEligibleAppointments(
        string status,
        bool archived,
        bool consent,
        string contactNumber,
        bool expectedEligible)
    {
        await using var fixture = await CreateFixtureAsync(
            status: status,
            archived: archived,
            consent: consent,
            contactNumber: contactNumber);

        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminders = await context.AppointmentSmsReminders
            .Where(item => item.AppointmentId == fixture.AppointmentId)
            .ToListAsync();

        if (!expectedEligible)
        {
            Assert.Empty(reminders);
            return;
        }

        Assert.Equal(2, reminders.Count);
        Assert.All(reminders, item =>
            Assert.Equal(AppointmentSmsReminderStatus.Pending, item.Status));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DisabledGlobalSetting_LeavesPendingRemindersUnskipped(
        bool smsEnabled,
        bool appointmentRemindersEnabled)
    {
        await using var fixture = await CreateFixtureAsync(
            smsEnabled: smsEnabled,
            appointmentRemindersEnabled: appointmentRemindersEnabled);

        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            FinalDueUtc.AddHours(1));

        var due = await fixture.Service.GetEligiblePendingDueAsync(
            FinalDueUtc.AddHours(1));

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminders = await context.AppointmentSmsReminders
            .Where(item => item.AppointmentId == fixture.AppointmentId)
            .ToListAsync();

        Assert.Equal(2, reminders.Count);
        Assert.All(reminders, item =>
            Assert.Equal(AppointmentSmsReminderStatus.Pending, item.Status));
        Assert.Empty(due);
    }

    [Fact]
    public async Task ReenabledSettings_AllowCatchUpAndKeepOnlyFinalReminderWhenBothAreDue()
    {
        await using var fixture = await CreateFixtureAsync(
            smsEnabled: false,
            appointmentRemindersEnabled: true);

        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            FinalDueUtc.AddHours(1));

        await fixture.SetSettingAsync(SystemSettingKeys.Notifications.SmsEnabled, "true");

        var due = await fixture.Service.GetEligiblePendingDueAsync(
            FinalDueUtc.AddHours(1));

        Assert.Single(due);
        Assert.Equal(AppointmentSmsReminderType.Final24Hour, due[0].ReminderType);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminders = await context.AppointmentSmsReminders
            .Where(item => item.AppointmentId == fixture.AppointmentId)
            .OrderBy(item => item.ReminderType)
            .ToListAsync();
        Assert.Equal(AppointmentSmsReminderStatus.Superseded,
            reminders.Single(item => item.ReminderType ==
                AppointmentSmsReminderType.Advance3Day).Status);
        Assert.Equal(AppointmentSmsReminderStatus.Pending,
            reminders.Single(item => item.ReminderType ==
                AppointmentSmsReminderType.Final24Hour).Status);
    }

    [Fact]
    public async Task OnlyAdvanceReminderOverdue_RemainsEligible()
    {
        await using var fixture = await CreateFixtureAsync();

        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            AdvanceDueUtc.AddHours(1));

        var due = await fixture.Service.GetEligiblePendingDueAsync(
            AdvanceDueUtc.AddHours(1));

        Assert.Single(due);
        Assert.Equal(AppointmentSmsReminderType.Advance3Day, due[0].ReminderType);
        Assert.Equal(DateTimeKind.Utc, due[0].ScheduledAtUtc.Kind);
        Assert.Equal(AdvanceDueUtc.Ticks, due[0].ScheduledAtUtc.Ticks);
    }

    [Fact]
    public async Task BothRemindersOverdue_SupersedesAdvanceAndKeepsFinalPending()
    {
        await using var fixture = await CreateFixtureAsync();

        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            FinalDueUtc.AddHours(1));

        var due = await fixture.Service.GetEligiblePendingDueAsync(
            FinalDueUtc.AddHours(1));

        Assert.Single(due);
        Assert.Equal(AppointmentSmsReminderType.Final24Hour, due[0].ReminderType);
    }

    [Fact]
    public async Task RestartCatchUp_ShortlyBeforeAppointment_ProcessesOnlyFinalReminder()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        var restartedService = fixture.CreateService();
        var due = await restartedService.GetEligiblePendingDueAsync(
            AppointmentAtUtc.AddMinutes(-30));

        Assert.Single(due);
        Assert.Equal(AppointmentSmsReminderType.Final24Hour, due[0].ReminderType);
    }

    [Fact]
    public async Task AppointmentAlreadyPassed_SkipsAllPendingReminders()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            AppointmentAtUtc.AddTicks(1));

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminders = await context.AppointmentSmsReminders
            .Where(item => item.AppointmentId == fixture.AppointmentId)
            .ToListAsync();
        Assert.Equal(2, reminders.Count);
        Assert.All(reminders, item =>
        {
            Assert.Equal(AppointmentSmsReminderStatus.Skipped, item.Status);
            Assert.Equal("appointment_passed", item.SanitizedOutcomeCode);
        });
    }

    [Fact]
    public async Task ConsentWithdrawalSuppressesOldGenerationAndReconsentCreatesNewRows()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await fixture.SetPatientConsentAsync(false, 2);
        await fixture.Service.ReconcilePatientAsync(
            fixture.PatientId,
            BeforeAdvanceDueUtc);

        await fixture.SetPatientConsentAsync(true, 3);
        await fixture.Service.ReconcilePatientAsync(
            fixture.PatientId,
            BeforeAdvanceDueUtc);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminders = await context.AppointmentSmsReminders
            .Where(item => item.AppointmentId == fixture.AppointmentId)
            .OrderBy(item => item.ConsentGeneration)
            .ThenBy(item => item.ReminderType)
            .ToListAsync();

        Assert.Equal(4, reminders.Count);
        Assert.All(reminders.Where(item => item.ConsentGeneration == 1), item =>
        {
            Assert.Equal(AppointmentSmsReminderStatus.Skipped, item.Status);
            Assert.Equal("consent_withdrawn", item.SanitizedOutcomeCode);
        });
        Assert.All(reminders.Where(item => item.ConsentGeneration == 3), item =>
            Assert.Equal(AppointmentSmsReminderStatus.Pending, item.Status));
    }

    [Fact]
    public async Task RescheduleAndMoveBack_SupersedeEachOldRevisionWithoutRevival()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await fixture.SetAppointmentScheduleAsync(
            new DateOnly(2026, 12, 8),
            new TimeOnly(9, 0),
            2);
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await fixture.SetAppointmentScheduleAsync(
            new DateOnly(2026, 12, 12),
            new TimeOnly(10, 0),
            3);
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await fixture.SetAppointmentScheduleAsync(
            new DateOnly(2026, 12, 10),
            new TimeOnly(9, 0),
            4);
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminders = await context.AppointmentSmsReminders
            .Where(item => item.AppointmentId == fixture.AppointmentId)
            .ToListAsync();

        Assert.Equal(8, reminders.Count);
        Assert.All(reminders.Where(item => item.ScheduleRevision is 1 or 2 or 3), item =>
            Assert.Equal(AppointmentSmsReminderStatus.Superseded, item.Status));
        Assert.All(reminders.Where(item => item.ScheduleRevision == 4), item =>
            Assert.Equal(AppointmentSmsReminderStatus.Pending, item.Status));
    }

    [Fact]
    public async Task TerminalAndSendingRows_AreNeverReactivatedOrReset()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await using (var context = await fixture.Database.Factory.CreateDbContextAsync())
        {
            var advance = await context.AppointmentSmsReminders.SingleAsync(item =>
                item.AppointmentId == fixture.AppointmentId &&
                item.ReminderType == AppointmentSmsReminderType.Advance3Day);
            advance.Status = AppointmentSmsReminderStatus.Failed;
            advance.FinalizedAtUtc = BeforeAdvanceDueUtc;

            var final = await context.AppointmentSmsReminders.SingleAsync(item =>
                item.AppointmentId == fixture.AppointmentId &&
                item.ReminderType == AppointmentSmsReminderType.Final24Hour);
            final.Status = AppointmentSmsReminderStatus.Sending;
            final.AttemptStartedAtUtc = BeforeAdvanceDueUtc;
            await context.SaveChangesAsync();
        }

        await fixture.SetPatientConsentAsync(false, 2);
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await using var verification = await fixture.Database.Factory.CreateDbContextAsync();
        var saved = await verification.AppointmentSmsReminders
            .Where(item => item.AppointmentId == fixture.AppointmentId)
            .ToListAsync();
        Assert.Equal(AppointmentSmsReminderStatus.Failed,
            saved.Single(item => item.ReminderType ==
                AppointmentSmsReminderType.Advance3Day).Status);
        Assert.Equal(AppointmentSmsReminderStatus.Sending,
            saved.Single(item => item.ReminderType ==
                AppointmentSmsReminderType.Final24Hour).Status);
    }

    [Fact]
    public async Task RepeatedAndConcurrentReconciliation_CreatesNoDuplicates()
    {
        await using var fixture = await CreateFixtureAsync();
        var secondService = fixture.CreateService();

        await Task.WhenAll(
            fixture.Service.ReconcileAppointmentAsync(
                fixture.AppointmentId,
                BeforeAdvanceDueUtc),
            secondService.ReconcileAppointmentAsync(
                fixture.AppointmentId,
                BeforeAdvanceDueUtc),
            fixture.Service.ReconcileAppointmentAsync(
                fixture.AppointmentId,
                BeforeAdvanceDueUtc));

        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        Assert.Equal(2, await context.AppointmentSmsReminders.CountAsync(
            item => item.AppointmentId == fixture.AppointmentId));
    }

    [Fact]
    public async Task RepositoryDuplicateInsert_IsIdempotentUnderUniqueConstraint()
    {
        await using var fixture = await CreateFixtureAsync();
        var first = NewPendingReminder(fixture.AppointmentId);
        var concurrentDuplicate = NewPendingReminder(fixture.AppointmentId);

        var repository = new AppointmentSmsReminderRepository(
            fixture.Database.Factory);
        await repository.CreatePendingIdempotentlyAsync(
            [first, concurrentDuplicate]);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        Assert.Equal(1, await context.AppointmentSmsReminders.CountAsync(item =>
            item.AppointmentId == fixture.AppointmentId &&
            item.ScheduleRevision == 1 &&
            item.ConsentGeneration == 1 &&
            item.ReminderType == AppointmentSmsReminderType.Advance3Day));
    }

    [Fact]
    public async Task AppointmentCreationAndRescheduleHooks_ReconcileReminders()
    {
        await using var fixture = await CreateFixtureAsync();
        AppointmentService appointmentService = fixture.CreateAppointmentService();

        Appointment created = await appointmentService.CreateAppointmentAsync(
            new Appointment
            {
                PatientId = fixture.PatientId,
                AppointmentDate = new DateOnly(2026, 12, 15),
                StartTime = new TimeOnly(11, 0),
                Purpose = "Consultation",
                Status = "Scheduled"
            });

        await using (var context = await fixture.Database.Factory.CreateDbContextAsync())
        {
            Assert.Equal(2, await context.AppointmentSmsReminders.CountAsync(
                item => item.AppointmentId == created.Id &&
                        item.ScheduleRevision == 1));
        }

        var edited = new Appointment
        {
            Id = created.Id,
            PatientId = created.PatientId,
            PregnancyId = created.PregnancyId,
            AppointmentDate = new DateOnly(2026, 12, 16),
            StartTime = created.StartTime,
            EndTime = created.EndTime,
            Purpose = created.Purpose,
            Notes = created.Notes,
            Status = created.Status
        };

        await appointmentService.UpdateAppointmentAsync(edited);

        await using var verification = await fixture.Database.Factory.CreateDbContextAsync();
        var rows = await verification.AppointmentSmsReminders
            .Where(item => item.AppointmentId == created.Id)
            .ToListAsync();
        Assert.Equal(4, rows.Count);
        Assert.All(rows.Where(item => item.ScheduleRevision == 1), item =>
            Assert.Equal(AppointmentSmsReminderStatus.Superseded, item.Status));
        Assert.All(rows.Where(item => item.ScheduleRevision == 2), item =>
            Assert.Equal(AppointmentSmsReminderStatus.Pending, item.Status));
    }

    [Fact]
    public async Task ConsentChangeHook_SkipsWithdrawnGenerationAndSchedulesReconsent()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        PatientService patientService = fixture.CreatePatientService();
        await patientService.SetSmsReminderConsentAsync(fixture.PatientId, false);
        await patientService.SetSmsReminderConsentAsync(fixture.PatientId, true);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var rows = await context.AppointmentSmsReminders
            .Where(item => item.AppointmentId == fixture.AppointmentId)
            .ToListAsync();
        Assert.Equal(4, rows.Count);
        Assert.All(rows.Where(item => item.ConsentGeneration == 1), item =>
            Assert.Equal(AppointmentSmsReminderStatus.Skipped, item.Status));
        Assert.All(rows.Where(item => item.ConsentGeneration == 3), item =>
            Assert.Equal(AppointmentSmsReminderStatus.Pending, item.Status));
    }

    [Fact]
    public async Task PendingDueQuery_TreatsSqliteUnspecifiedTimestampAsUtc()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.ReconcileAppointmentAsync(
            fixture.AppointmentId,
            BeforeAdvanceDueUtc);

        await using (var context = await fixture.Database.Factory.CreateDbContextAsync())
        {
            var stored = await context.AppointmentSmsReminders.SingleAsync(item =>
                item.AppointmentId == fixture.AppointmentId &&
                item.ReminderType == AppointmentSmsReminderType.Advance3Day);
            Assert.Equal(DateTimeKind.Unspecified, stored.ScheduledAtUtc.Kind);
        }

        var due = await fixture.Service.GetEligiblePendingDueAsync(
            AdvanceDueUtc.AddHours(1));

        Assert.Single(due);
        Assert.Equal(AppointmentSmsReminderType.Advance3Day, due[0].ReminderType);
    }

    private static async Task<ReminderFixture> CreateFixtureAsync(
        string status = "Scheduled",
        bool archived = false,
        bool consent = true,
        string contactNumber = "09123456789",
        bool smsEnabled = true,
        bool appointmentRemindersEnabled = true)
    {
        var database = await TestDatabase.CreateAsync();
        try
        {
            await using var context = await database.Factory.CreateDbContextAsync();
            const string username = "reminder-service-test";
            var staff = new Staff
            {
                Username = username,
                NormalizedUsername = username.ToUpperInvariant(),
                PasswordHash = "test-hash",
                Role = StaffRoles.Admin,
                IsActive = true
            };
            context.StaffMembers.Add(staff);

            var patient = new Patient
            {
                PatientNumber = $"REM-{Guid.NewGuid():N}"[..12],
                FirstName = "Reminder",
                LastName = "Patient",
                ContactNumber = contactNumber,
                HasSmsReminderConsent = consent,
                SmsReminderConsentGeneration = consent ? 1 : 0
            };
            var appointment = new Appointment
            {
                Patient = patient,
                AppointmentDate = new DateOnly(2026, 12, 10),
                StartTime = new TimeOnly(9, 0),
                Purpose = "Prenatal consultation",
                Status = status,
                IsArchived = archived,
                ScheduleRevision = 1
            };
            context.Appointments.Add(appointment);
            context.SystemSettings.AddRange(
                Setting(SystemSettingKeys.Notifications.SmsEnabled, smsEnabled),
                Setting(SystemSettingKeys.Notifications.AppointmentReminders,
                    appointmentRemindersEnabled),
                new SystemSetting
                {
                    Key = SystemSettingKeys.Appointments.DefaultDuration,
                    Value = "30",
                    UpdatedAtUtc = BeforeAdvanceDueUtc
                });
            await context.SaveChangesAsync();

            return new ReminderFixture(
                database,
                appointment.Id,
                patient.Id,
                staff.Id,
                username,
                staff.SecurityStamp);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    private static SystemSetting Setting(string key, bool value) => new()
    {
        Key = key,
        Value = value.ToString().ToLowerInvariant(),
        UpdatedAtUtc = BeforeAdvanceDueUtc
    };

    private static AppointmentSmsReminder NewPendingReminder(int appointmentId) => new()
    {
        AppointmentId = appointmentId,
        ScheduleRevision = 1,
        ConsentGeneration = 1,
        ReminderType = AppointmentSmsReminderType.Advance3Day,
        ScheduledAtUtc = AdvanceDueUtc,
        AppointmentDateSnapshot = new DateOnly(2026, 12, 10),
        AppointmentStartTimeSnapshot = new TimeOnly(9, 0),
        CreatedAtUtc = BeforeAdvanceDueUtc,
        StatusChangedAtUtc = BeforeAdvanceDueUtc
    };

    private sealed class ReminderFixture : IAsyncDisposable
    {
        public TestDatabase Database { get; }
        public int AppointmentId { get; }
        public int PatientId { get; }
        private int StaffId { get; }
        private string Username { get; }
        private string SecurityStamp { get; }
        public AppointmentSmsReminderService Service { get; }

        public ReminderFixture(
            TestDatabase database,
            int appointmentId,
            int patientId,
            int staffId,
            string username,
            string securityStamp)
        {
            Database = database;
            AppointmentId = appointmentId;
            PatientId = patientId;
            StaffId = staffId;
            Username = username;
            SecurityStamp = securityStamp;
            Service = CreateService();
        }

        public AppointmentSmsReminderService CreateService() => new(
            new AppointmentSmsReminderRepository(Database.Factory),
            new SystemSettingRepository(Database.Factory));

        public AppointmentService CreateAppointmentService()
        {
            var session = CreateSession();
            var authorization = new AuthorizationService(
                session,
                new StaffRepository(Database.Factory));
            var settings = new SystemSettingRepository(Database.Factory);
            var settingService = new SystemSettingService(
                settings,
                authorization,
                session);

            return new AppointmentService(
                new AppointmentRepository(Database.Factory),
                new PatientRepository(Database.Factory),
                new PregnancyRepository(Database.Factory),
                authorization,
                session,
                new RegionalSettingsService(settings),
                settingService,
                Service);
        }

        public PatientService CreatePatientService()
        {
            var session = CreateSession();
            return new PatientService(
                new PatientRepository(Database.Factory),
                new AuthorizationService(
                    session,
                    new StaffRepository(Database.Factory)),
                session,
                Service);
        }

        private SessionService CreateSession()
        {
            var session = new SessionService();
            session.StartSession(new UserSession(
                StaffId,
                Username,
                StaffRoles.Admin,
                SecurityStamp));
            return session;
        }

        public async Task SetSettingAsync(string key, string value)
        {
            await using var context = await Database.Factory.CreateDbContextAsync();
            var setting = await context.SystemSettings.SingleAsync(item => item.Key == key);
            setting.Value = value;
            setting.UpdatedAtUtc = BeforeAdvanceDueUtc;
            await context.SaveChangesAsync();
        }

        public async Task SetPatientConsentAsync(bool hasConsent, int generation)
        {
            await using var context = await Database.Factory.CreateDbContextAsync();
            var patient = await context.Patients.SingleAsync(item => item.Id == PatientId);
            patient.HasSmsReminderConsent = hasConsent;
            patient.SmsReminderConsentGeneration = generation;
            await context.SaveChangesAsync();
        }

        public async Task SetAppointmentScheduleAsync(
            DateOnly date,
            TimeOnly time,
            int revision)
        {
            await using var context = await Database.Factory.CreateDbContextAsync();
            var appointment = await context.Appointments.SingleAsync(
                item => item.Id == AppointmentId);
            appointment.AppointmentDate = date;
            appointment.StartTime = time;
            appointment.ScheduleRevision = revision;
            await context.SaveChangesAsync();
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
