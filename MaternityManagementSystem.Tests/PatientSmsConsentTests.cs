using System.Text.Json;
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

public class PatientSmsConsentTests
{
    private const string LastMigrationBeforeConsent =
        "20261126094753_AddLaboratoryResultWorkflow";

    [Fact]
    public async Task RegisterPatientAsync_DefaultsToNoSmsReminderConsent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreateAuthenticatedServiceAsync(database);

        Patient patient = await service.RegisterPatientAsync(CreatePatient());

        Assert.False(patient.HasSmsReminderConsent);
        Assert.Null(patient.SmsReminderConsentRecordedAtUtc);
        Assert.Null(patient.SmsReminderConsentRecordedByStaffId);

        await using var context = await database.Factory.CreateDbContextAsync();
        Patient saved = await context.Patients.SingleAsync(item => item.Id == patient.Id);
        Assert.False(saved.HasSmsReminderConsent);
        Assert.Null(saved.SmsReminderConsentRecordedAtUtc);
        Assert.Null(saved.SmsReminderConsentRecordedByStaffId);
    }

    [Fact]
    public async Task RegisterPatientAsync_WhenConsentWasExplicitlySelected_RecordsConsentAndStaff()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreateAuthenticatedServiceAsync(database);
        Patient patientToRegister = CreatePatient();
        patientToRegister.HasSmsReminderConsent = true;
        DateTime beforeRegistrationUtc = DateTime.UtcNow;

        Patient patient = await service.RegisterPatientAsync(patientToRegister);

        DateTime afterRegistrationUtc = DateTime.UtcNow;
        Assert.True(patient.HasSmsReminderConsent);
        Assert.NotNull(patient.SmsReminderConsentRecordedAtUtc);
        Assert.InRange(
            patient.SmsReminderConsentRecordedAtUtc!.Value,
            beforeRegistrationUtc,
            afterRegistrationUtc);
        Assert.Equal(1, patient.SmsReminderConsentRecordedByStaffId);

        await using var context = await database.Factory.CreateDbContextAsync();
        AuditLog creationLog = await context.AuditLogs.SingleAsync(
            item => item.EntityType == "Patient" &&
                    item.EntityId == patient.Id &&
                    item.Action == "Created");
        Assert.Contains("consent", creationLog.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(patient.SmsReminderConsentRecordedByStaffId, creationLog.StaffId);
    }

    [Fact]
    public async Task SetSmsReminderConsentAsync_GrantAndWithdrawalRecordStaffAndVersions()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreateAuthenticatedServiceAsync(database);
        Patient patient = await service.RegisterPatientAsync(CreatePatient());

        DateTime beforeGrantUtc = DateTime.UtcNow;
        Patient granted = await service.SetSmsReminderConsentAsync(
            patient.Id,
            hasConsent: true);
        DateTime afterGrantUtc = DateTime.UtcNow;

        Assert.True(granted.HasSmsReminderConsent);
        Assert.NotNull(granted.SmsReminderConsentRecordedAtUtc);
        Assert.InRange(
            granted.SmsReminderConsentRecordedAtUtc!.Value,
            beforeGrantUtc,
            afterGrantUtc);
        Assert.Equal(1, granted.SmsReminderConsentRecordedByStaffId);

        DateTime beforeWithdrawalUtc = DateTime.UtcNow;
        Patient withdrawn = await service.SetSmsReminderConsentAsync(
            patient.Id,
            hasConsent: false);
        DateTime afterWithdrawalUtc = DateTime.UtcNow;

        Assert.False(withdrawn.HasSmsReminderConsent);
        Assert.NotNull(withdrawn.SmsReminderConsentRecordedAtUtc);
        Assert.InRange(
            withdrawn.SmsReminderConsentRecordedAtUtc!.Value,
            beforeWithdrawalUtc,
            afterWithdrawalUtc);
        Assert.Equal(1, withdrawn.SmsReminderConsentRecordedByStaffId);

        await using var context = await database.Factory.CreateDbContextAsync();
        var consentLogs = await context.AuditLogs
            .Where(item => item.EntityId == patient.Id &&
                           item.EntityType == "Patient" &&
                           item.Action.StartsWith("SMS Reminder Consent"))
            .OrderBy(item => item.OccurredAtUtc)
            .ToListAsync();

        Assert.Equal(2, consentLogs.Count);
        Assert.Equal("SMS Reminder Consent Granted", consentLogs[0].Action);
        Assert.Equal("SMS Reminder Consent Withdrawn", consentLogs[1].Action);
        Assert.All(consentLogs, item => Assert.Equal(1, item.StaffId));

        Patient savedPatient = await context.Patients
            .SingleAsync(item => item.Id == patient.Id);
        Assert.False(savedPatient.HasSmsReminderConsent);
        Assert.Equal(
            withdrawn.SmsReminderConsentRecordedByStaffId,
            savedPatient.SmsReminderConsentRecordedByStaffId);
        Assert.Equal(
            withdrawn.SmsReminderConsentRecordedAtUtc,
            savedPatient.SmsReminderConsentRecordedAtUtc);

        var versions = await context.RecordVersions
            .Where(item => item.EntityType == "Patient" && item.EntityId == patient.Id)
            .OrderBy(item => item.ChangedAtUtc)
            .ToListAsync();

        Assert.Equal(2, versions.Count);
        JsonElement grantedValues = JsonDocument.Parse(
            versions[0].NewValuesJson).RootElement;
        Assert.True(grantedValues.GetProperty("HasSmsReminderConsent").GetBoolean());
        Assert.Equal(1, grantedValues.GetProperty(
            "SmsReminderConsentRecordedByStaffId").GetInt32());

        JsonElement withdrawnValues = JsonDocument.Parse(
            versions[1].NewValuesJson).RootElement;
        Assert.False(withdrawnValues.GetProperty("HasSmsReminderConsent").GetBoolean());
        Assert.Equal(1, withdrawnValues.GetProperty(
            "SmsReminderConsentRecordedByStaffId").GetInt32());
    }

    [Fact]
    public async Task SetSmsReminderConsentAsync_RejectsUnauthenticatedChanges()
    {
        await using var database = await TestDatabase.CreateAsync();
        Patient patient;

        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            patient = CreatePatient();
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
        }

        var session = new SessionService();
        var service = new PatientService(
            new PatientRepository(database.Factory),
            new AuthorizationService(
                session,
                new StaffRepository(database.Factory)),
            session);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SetSmsReminderConsentAsync(patient.Id, true));

        await using var verificationContext =
            await database.Factory.CreateDbContextAsync();
        Patient unchanged = await verificationContext.Patients
            .SingleAsync(item => item.Id == patient.Id);
        Assert.False(unchanged.HasSmsReminderConsent);
        Assert.Null(unchanged.SmsReminderConsentRecordedAtUtc);
        Assert.Empty(await verificationContext.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task UpdatePatientAsync_CannotGrantConsentThroughOrdinaryProfileSave()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = await CreateAuthenticatedServiceAsync(database);
        Patient patient = await service.RegisterPatientAsync(CreatePatient());

        var profileEdit = new Patient
        {
            Id = patient.Id,
            PatientNumber = patient.PatientNumber,
            FirstName = "Updated",
            LastName = patient.LastName,
            ContactNumber = patient.ContactNumber,
            CreatedAtUtc = patient.CreatedAtUtc,
            HasSmsReminderConsent = true,
            SmsReminderConsentRecordedAtUtc = DateTime.UtcNow,
            SmsReminderConsentRecordedByStaffId = 999
        };

        await service.UpdatePatientAsync(profileEdit);

        await using var context = await database.Factory.CreateDbContextAsync();
        Patient saved = await context.Patients.SingleAsync(item => item.Id == patient.Id);
        Assert.Equal("Updated", saved.FirstName);
        Assert.False(saved.HasSmsReminderConsent);
        Assert.Null(saved.SmsReminderConsentRecordedAtUtc);
        Assert.Null(saved.SmsReminderConsentRecordedByStaffId);

        Assert.Empty(await context.AuditLogs.Where(item =>
            item.EntityId == patient.Id &&
            item.Action.StartsWith("SMS Reminder Consent")).ToListAsync());
    }

    [Fact]
    public async Task AddPatientSmsReminderConsentMigration_DefaultsExistingRowsToFalse()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"GolddieConsentMigration_{Guid.NewGuid():N}.db");

        try
        {
            var factory = new TestClinicDbContextFactory(databasePath);

            await using (var context = await factory.CreateDbContextAsync())
            {
                IMigrator migrator = context.GetService<IMigrator>();
                await migrator.MigrateAsync(LastMigrationBeforeConsent);

                await context.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "Patients"
                        ("PatientNumber", "FirstName", "LastName", "CreatedAtUtc")
                    VALUES
                        ('LEGACY-001', 'Legacy', 'Patient', '2025-01-01 00:00:00');
                    """);

                await migrator.MigrateAsync();
            }

            await using (var verificationContext =
                         await factory.CreateDbContextAsync())
            {
                Patient legacy = await verificationContext.Patients
                    .SingleAsync(item => item.PatientNumber == "LEGACY-001");

                Assert.False(legacy.HasSmsReminderConsent);
                Assert.Null(legacy.SmsReminderConsentRecordedAtUtc);
                Assert.Null(legacy.SmsReminderConsentRecordedByStaffId);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-journal");
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");
        }
    }

    private static async Task<PatientService> CreateAuthenticatedServiceAsync(
        TestDatabase database)
    {
        int staffId;
        string securityStamp;
        const string username = "patient-consent-test-user";

        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = username,
                NormalizedUsername = username.ToUpperInvariant(),
                PasswordHash = "test-password-hash",
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
            staffId,
            username,
            StaffRoles.Staff,
            securityStamp));

        return new PatientService(
            new PatientRepository(database.Factory),
            new AuthorizationService(
                session,
                new StaffRepository(database.Factory)),
            session);
    }

    private static Patient CreatePatient()
    {
        return new Patient
        {
            PatientNumber = $"CONSENT-{Guid.NewGuid():N}",
            FirstName = "Test",
            LastName = "Patient"
        };
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
