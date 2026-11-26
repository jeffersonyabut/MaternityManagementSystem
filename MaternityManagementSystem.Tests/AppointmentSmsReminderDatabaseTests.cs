using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class AppointmentSmsReminderDatabaseTests
{
    private const string PreviousMigration =
        "20261126202803_AddPatientSmsReminderConsent";

    private const string CurrentMigration =
        "20261126204239_AddAppointmentSmsReminders";

    private static async Task<AppointmentService> CreateServiceAsync(
        TestDatabase database)
    {
        const string username = "reminder-schema-test";
        int staffId;
        string securityStamp;

        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = username,
                NormalizedUsername = username.ToUpperInvariant(),
                PasswordHash = "test-hash",
                Role = StaffRoles.Admin,
                IsActive = true
            };
            context.StaffMembers.Add(staff);
            context.SystemSettings.Add(new SystemSetting
            {
                Key = SystemSettingKeys.Appointments.DefaultDuration,
                Value = "30"
            });
            await context.SaveChangesAsync();
            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var session = new SessionService();
        session.StartSession(new UserSession(
            staffId,
            username,
            StaffRoles.Admin,
            securityStamp));

        var settings = new SystemSettingRepository(database.Factory);
        var authorization = new AuthorizationService(
            session,
            new StaffRepository(database.Factory));

        return new AppointmentService(
            new AppointmentRepository(database.Factory),
            new PatientRepository(database.Factory),
            new PregnancyRepository(database.Factory),
            authorization,
            session,
            new RegionalSettingsService(settings),
            new SystemSettingService(settings, authorization, session));
    }

    private static async Task<int> CreatePatientAsync(TestDatabase database)
    {
        await using var context = await database.Factory.CreateDbContextAsync();
        var patient = new Patient
        {
            PatientNumber = $"REM-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Patient"
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient.Id;
    }

    private static Appointment NewAppointment(int patientId) => new()
    {
        PatientId = patientId,
        AppointmentDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
        StartTime = new TimeOnly(10, 0),
        Purpose = "Prenatal consultation",
        Status = "Scheduled"
    };

    private static AppointmentSmsReminder NewReminder(
        int appointmentId,
        AppointmentSmsReminderType type) => new()
    {
        AppointmentId = appointmentId,
        ScheduleRevision = 1,
        ReminderType = type,
        ScheduledAtUtc = new DateTime(2026, 12, 1, 1, 0, 0, DateTimeKind.Utc),
        AppointmentDateSnapshot = new DateOnly(2026, 12, 4),
        AppointmentStartTimeSnapshot = new TimeOnly(9, 0),
        Status = AppointmentSmsReminderStatus.Pending,
        CreatedAtUtc = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc),
        StatusChangedAtUtc = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task NewAppointments_StartAtScheduleRevisionOne_AndDateChangeIncrementsIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreateServiceAsync(database);
        int patientId = await CreatePatientAsync(database);

        var created = await service.CreateAppointmentAsync(NewAppointment(patientId));
        Assert.Equal(1, created.ScheduleRevision);

        var edited = new Appointment
        {
            Id = created.Id,
            PatientId = created.PatientId,
            PregnancyId = created.PregnancyId,
            AppointmentDate = created.AppointmentDate.AddDays(1),
            StartTime = created.StartTime,
            EndTime = created.EndTime,
            Purpose = created.Purpose,
            Notes = created.Notes,
            Status = created.Status
        };

        var updated = await service.UpdateAppointmentAsync(edited);
        Assert.Equal(2, updated.ScheduleRevision);

        await using var context = await database.Factory.CreateDbContextAsync();
        Assert.Equal(2, (await context.Appointments.SingleAsync()).ScheduleRevision);
    }

    [Fact]
    public async Task ChangingStartTime_IncrementsScheduleRevision()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreateServiceAsync(database);
        int patientId = await CreatePatientAsync(database);
        var created = await service.CreateAppointmentAsync(NewAppointment(patientId));

        var update = new Appointment
        {
            Id = created.Id,
            PatientId = created.PatientId,
            AppointmentDate = created.AppointmentDate,
            StartTime = created.StartTime.AddMinutes(30),
            EndTime = created.EndTime?.AddMinutes(30),
            Purpose = created.Purpose,
            Status = created.Status
        };

        Assert.Equal(2, (await service.UpdateAppointmentAsync(update)).ScheduleRevision);
    }

    [Fact]
    public async Task UnrelatedAppointmentEdit_DoesNotIncrementScheduleRevision()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreateServiceAsync(database);
        int patientId = await CreatePatientAsync(database);
        var created = await service.CreateAppointmentAsync(NewAppointment(patientId));

        var update = new Appointment
        {
            Id = created.Id,
            PatientId = created.PatientId,
            AppointmentDate = created.AppointmentDate,
            StartTime = created.StartTime,
            EndTime = created.EndTime,
            Purpose = "Updated purpose",
            Status = created.Status
        };

        Assert.Equal(1, (await service.UpdateAppointmentAsync(update)).ScheduleRevision);
    }

    [Fact]
    public async Task ReminderUniqueKey_AllowsDifferentTypesButRejectsDuplicateType()
    {
        await using var database = await TestDatabase.CreateAsync();
        int patientId = await CreatePatientAsync(database);
        await using var context = await database.Factory.CreateDbContextAsync();
        var appointment = NewAppointment(patientId);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        context.AppointmentSmsReminders.AddRange(
            NewReminder(appointment.Id, AppointmentSmsReminderType.Advance3Day),
            NewReminder(appointment.Id, AppointmentSmsReminderType.Final24Hour));
        await context.SaveChangesAsync();

        context.AppointmentSmsReminders.Add(
            NewReminder(appointment.Id, AppointmentSmsReminderType.Advance3Day));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ReminderStatusUtcFieldsAndProviderReference_Persist()
    {
        await using var database = await TestDatabase.CreateAsync();
        int patientId = await CreatePatientAsync(database);
        int appointmentId;
        var scheduledAtUtc = new DateTime(2026, 12, 1, 1, 2, 3, DateTimeKind.Utc);

        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            var appointment = NewAppointment(patientId);
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();
            appointmentId = appointment.Id;
            var reminder = NewReminder(appointmentId, AppointmentSmsReminderType.Final24Hour);
            reminder.Status = AppointmentSmsReminderStatus.ProviderAccepted;
            reminder.ScheduledAtUtc = scheduledAtUtc;
            reminder.AttemptStartedAtUtc = scheduledAtUtc.AddMinutes(1);
            reminder.FinalizedAtUtc = scheduledAtUtc.AddMinutes(2);
            reminder.ProviderReference = "provider-ref-1";
            reminder.ProviderStatus = "accepted";
            reminder.SanitizedOutcomeCode = "accepted";
            context.AppointmentSmsReminders.Add(reminder);
            await context.SaveChangesAsync();
        }

        await using var verify = await database.Factory.CreateDbContextAsync();
        var saved = await verify.AppointmentSmsReminders.SingleAsync();
        Assert.Equal(AppointmentSmsReminderStatus.ProviderAccepted, saved.Status);
        Assert.Equal(scheduledAtUtc, saved.ScheduledAtUtc);
        Assert.Equal("provider-ref-1", saved.ProviderReference);
    }

    [Fact]
    public async Task ReminderForeignKey_RejectsMissingAppointment_AndRestrictsDelete()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = await database.Factory.CreateDbContextAsync();

        context.AppointmentSmsReminders.Add(
            NewReminder(99999, AppointmentSmsReminderType.Advance3Day));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        int patientId = await CreatePatientAsync(database);
        var appointment = NewAppointment(patientId);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();
        context.AppointmentSmsReminders.Add(
            NewReminder(appointment.Id, AppointmentSmsReminderType.Advance3Day));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<SqliteException>(() =>
            context.Database.ExecuteSqlRawAsync(
                "DELETE FROM Appointments WHERE Id = {0}", appointment.Id));
    }

    [Fact]
    public async Task Migration_UpAndDown_PreservesExistingAppointmentsAtRevisionOne()
    {
        string path = Path.Combine(Path.GetTempPath(), $"GolddieMigration_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using var context = new ClinicDbContext(options);
            await context.Database.MigrateAsync(PreviousMigration);
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Patients (PatientNumber, FirstName, LastName, CreatedAtUtc) VALUES ('MIG-001', 'Legacy', 'Patient', '2026-01-01 00:00:00');");
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Appointments (PatientId, AppointmentDate, StartTime, Purpose, Status, CreatedAtUtc, IsArchived) VALUES (1, '2026-12-01', '09:00:00', 'Existing appointment', 'Scheduled', '2026-01-01 00:00:00', 0);");

            await context.Database.MigrateAsync(CurrentMigration);
            Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
                "SELECT ScheduleRevision AS Value FROM Appointments WHERE Id = 1").SingleAsync());
            Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM AppointmentSmsReminders").SingleAsync());

            await context.Database.MigrateAsync(PreviousMigration);
            Assert.DoesNotContain(
                CurrentMigration,
                await context.Database.GetAppliedMigrationsAsync());
            Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'AppointmentSmsReminders'")
                .SingleAsync());
            Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('Appointments') WHERE name = 'ScheduleRevision'")
                .SingleAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (string suffix in new[] { "", "-journal", "-wal", "-shm" })
            {
                string candidate = path + suffix;
                if (File.Exists(candidate)) File.Delete(candidate);
            }
        }
    }
}
