using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class PatientSmsConsentGenerationTests
{
    private const string PreviousMigration =
        "20261126204239_AddAppointmentSmsReminders";

    private const string GenerationMigration =
        "20261126210209_AddPatientSmsConsentGeneration";

    [Fact]
    public async Task ConsentGeneration_ChangesOnlyWhenConsentStateChanges()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreatePatientServiceAsync(database);
        Patient patient = await service.RegisterPatientAsync(NewPatient());

        Assert.Equal(0, patient.SmsReminderConsentGeneration);

        patient = await service.SetSmsReminderConsentAsync(patient.Id, true);
        Assert.Equal(1, patient.SmsReminderConsentGeneration);

        patient = await service.SetSmsReminderConsentAsync(patient.Id, true);
        Assert.Equal(1, patient.SmsReminderConsentGeneration);

        patient = await service.SetSmsReminderConsentAsync(patient.Id, false);
        Assert.Equal(2, patient.SmsReminderConsentGeneration);

        patient = await service.SetSmsReminderConsentAsync(patient.Id, false);
        Assert.Equal(2, patient.SmsReminderConsentGeneration);

        patient = await service.SetSmsReminderConsentAsync(patient.Id, true);
        Assert.Equal(3, patient.SmsReminderConsentGeneration);

        await using var context = await database.Factory.CreateDbContextAsync();
        Patient saved = await context.Patients.SingleAsync(item => item.Id == patient.Id);
        Assert.True(saved.HasSmsReminderConsent);
        Assert.Equal(3, saved.SmsReminderConsentGeneration);
    }

    [Fact]
    public async Task OrdinaryProfileEdit_DoesNotChangeConsentGeneration()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreatePatientServiceAsync(database);
        Patient patient = await service.RegisterPatientAsync(NewPatient());
        patient = await service.SetSmsReminderConsentAsync(patient.Id, true);

        var edit = new Patient
        {
            Id = patient.Id,
            PatientNumber = patient.PatientNumber,
            FirstName = "Updated",
            LastName = patient.LastName,
            CreatedAtUtc = patient.CreatedAtUtc,
            HasSmsReminderConsent = false,
            SmsReminderConsentGeneration = 999
        };

        await service.UpdatePatientAsync(edit);

        await using var context = await database.Factory.CreateDbContextAsync();
        Patient saved = await context.Patients.SingleAsync(item => item.Id == patient.Id);
        Assert.True(saved.HasSmsReminderConsent);
        Assert.Equal(1, saved.SmsReminderConsentGeneration);
    }

    [Fact]
    public async Task ReminderUniqueKey_AllowsDifferentConsentGenerationsAndRejectsDuplicates()
    {
        await using var database = await TestDatabase.CreateAsync();
        int patientId = await CreatePatientAsync(database);
        await using var context = await database.Factory.CreateDbContextAsync();
        var appointment = NewAppointment(patientId);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        context.AppointmentSmsReminders.AddRange(
            NewReminder(appointment.Id, 0),
            NewReminder(appointment.Id, 1));
        await context.SaveChangesAsync();

        context.AppointmentSmsReminders.Add(NewReminder(appointment.Id, 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task MigrationUpAndDown_PreservesPatientAndReminderDataWhenCollisionFree()
    {
        string path = NewTemporaryDatabasePath();
        try
        {
            var factory = new TestClinicDbContextFactory(path);
            await SeedLegacyRowsAndMigrateUpAsync(factory);

            await using (var context = await factory.CreateDbContextAsync())
            {
                Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                    "SELECT SmsReminderConsentGeneration AS Value FROM Patients WHERE Id = 1")
                    .SingleAsync());
                Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                    "SELECT ConsentGeneration AS Value FROM AppointmentSmsReminders WHERE Id = 1")
                    .SingleAsync());
                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

                Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM AppointmentSmsReminders WHERE ProviderReference = 'legacy-ref'")
                    .SingleAsync());
                Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM Patients WHERE PatientNumber = 'GEN-LEGACY'")
                    .SingleAsync());
                Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = 'IX_AppointmentSmsReminders_AppointmentId_ScheduleRevision_ReminderType'")
                    .SingleAsync());
                Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM pragma_table_info('Patients') WHERE name = 'SmsReminderConsentGeneration'")
                    .SingleAsync());
                Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM pragma_table_info('AppointmentSmsReminders') WHERE name = 'ConsentGeneration'")
                    .SingleAsync());
            }
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    [Fact]
    public async Task MigrationDown_WithOldKeyCollisions_FailsWithoutChangingSchemaOrRows()
    {
        string path = NewTemporaryDatabasePath();
        try
        {
            var factory = new TestClinicDbContextFactory(path);
            await SeedLegacyRowsAndMigrateUpAsync(factory);

            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO AppointmentSmsReminders (AppointmentId, ScheduleRevision, ConsentGeneration, ReminderType, ScheduledAtUtc, AppointmentDateSnapshot, AppointmentStartTimeSnapshot, Status, CreatedAtUtc, StatusChangedAtUtc, ProviderReference) VALUES (1, 1, 1, 'Advance3Day', '2026-12-01 01:00:00', '2026-12-04', '09:00:00', 'Skipped', '2026-11-27 01:00:00', '2026-11-27 01:00:00', 'new-consent-ref')");

                var exception = await Assert.ThrowsAsync<SqliteException>(
                    () => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
                Assert.Contains("Cannot downgrade SMS consent generations", exception.Message);

                Assert.Contains(
                    GenerationMigration,
                    await context.Database.GetAppliedMigrationsAsync());
                Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = 'IX_AppointmentSmsReminders_AppointmentId_ScheduleRevision_ConsentGeneration_ReminderType'")
                    .SingleAsync());
                Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = 'IX_AppointmentSmsReminders_AppointmentId_ScheduleRevision_ReminderType'")
                    .SingleAsync());
                Assert.Equal(2, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM AppointmentSmsReminders WHERE AppointmentId = 1 AND ScheduleRevision = 1 AND ReminderType = 'Advance3Day'")
                    .SingleAsync());
                Assert.Equal(new[] { 0, 1 }, await context.Database.SqlQueryRaw<int>(
                    "SELECT ConsentGeneration AS Value FROM AppointmentSmsReminders WHERE AppointmentId = 1 AND ScheduleRevision = 1 AND ReminderType = 'Advance3Day' ORDER BY ConsentGeneration")
                    .ToArrayAsync());
                Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM AppointmentSmsReminders WHERE ProviderReference = 'legacy-ref' AND Status = 'Pending'")
                    .SingleAsync());
                Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                    "SELECT SmsReminderConsentGeneration AS Value FROM Patients WHERE Id = 1")
                    .SingleAsync());
                Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM pragma_table_info('Patients') WHERE name = 'SmsReminderConsentGeneration'")
                    .SingleAsync());
                Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM pragma_table_info('AppointmentSmsReminders') WHERE name = 'ConsentGeneration'")
                    .SingleAsync());
                Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_temp_master WHERE name = '__ConsentGenerationDowngradeGuard' OR name = '__ConsentGenerationDowngradeGuardTrigger'")
                    .SingleAsync());
            }
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    private static async Task SeedLegacyRowsAndMigrateUpAsync(
        TestClinicDbContextFactory factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Patients (PatientNumber, FirstName, LastName, CreatedAtUtc) VALUES ('GEN-LEGACY', 'Legacy', 'Patient', '2026-01-01 00:00:00')");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Appointments (PatientId, AppointmentDate, StartTime, Purpose, Status, CreatedAtUtc, IsArchived) VALUES (1, '2026-12-04', '09:00:00', 'Consultation', 'Scheduled', '2026-01-01 00:00:00', 0)");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO AppointmentSmsReminders (AppointmentId, ScheduleRevision, ReminderType, ScheduledAtUtc, AppointmentDateSnapshot, AppointmentStartTimeSnapshot, Status, CreatedAtUtc, StatusChangedAtUtc, ProviderReference) VALUES (1, 1, 'Advance3Day', '2026-12-01 01:00:00', '2026-12-04', '09:00:00', 'Pending', '2026-11-27 01:00:00', '2026-11-27 01:00:00', 'legacy-ref')");
        await migrator.MigrateAsync(GenerationMigration);
    }

    private static async Task<PatientService> CreatePatientServiceAsync(
        TestDatabase database)
    {
        const string username = "consent-generation-test";
        int staffId;
        string securityStamp;
        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = username,
                NormalizedUsername = username.ToUpperInvariant(),
                PasswordHash = "test-hash",
                Role = StaffRoles.Staff,
                IsActive = true
            };
            context.StaffMembers.Add(staff);
            await context.SaveChangesAsync();
            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var session = new SessionService();
        session.StartSession(new UserSession(
            staffId, username, StaffRoles.Staff, securityStamp));
        return new PatientService(
            new PatientRepository(database.Factory),
            new AuthorizationService(session, new StaffRepository(database.Factory)),
            session);
    }

    private static async Task<int> CreatePatientAsync(TestDatabase database)
    {
        await using var context = await database.Factory.CreateDbContextAsync();
        var patient = NewPatient();
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient.Id;
    }

    private static Patient NewPatient() => new()
    {
        PatientNumber = $"GEN-{Guid.NewGuid():N}"[..12],
        FirstName = "Test",
        LastName = "Patient"
    };

    private static Appointment NewAppointment(int patientId) => new()
    {
        PatientId = patientId,
        AppointmentDate = new DateOnly(2026, 12, 4),
        StartTime = new TimeOnly(9, 0),
        Purpose = "Consultation",
        Status = "Scheduled"
    };

    private static AppointmentSmsReminder NewReminder(int appointmentId, int consentGeneration) => new()
    {
        AppointmentId = appointmentId,
        ScheduleRevision = 1,
        ConsentGeneration = consentGeneration,
        ReminderType = AppointmentSmsReminderType.Advance3Day,
        ScheduledAtUtc = new DateTime(2026, 12, 1, 1, 0, 0, DateTimeKind.Utc),
        AppointmentDateSnapshot = new DateOnly(2026, 12, 4),
        AppointmentStartTimeSnapshot = new TimeOnly(9, 0)
    };

    private static string NewTemporaryDatabasePath() => Path.Combine(
        Path.GetTempPath(),
        $"GolddieConsentGeneration_{Guid.NewGuid():N}.db");

    private static void DeleteTemporaryDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { "", "-journal", "-wal", "-shm" })
        {
            string candidate = path + suffix;
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
