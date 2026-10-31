
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class PatientServiceTests
{
    private static async Task<PatientService> CreateAuthenticatedServiceAsync(
        TestDatabase testDatabase)
    {
        int staffId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = "patient-test-user",
                PasswordHash = "test-password-hash",
                Role = "Nurse/Midwife",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.StaffMembers.Add(staff);
            await dbContext.SaveChangesAsync();

            staffId = staff.Id;
        }

        var sessionService = new SessionService();

        sessionService.StartSession(
            new UserSession(
                staffId,
                "patient-test-user",
                "Nurse/Midwife"));

        var authorizationService =
            new AuthorizationService(sessionService);

        return new PatientService(
            new PatientRepository(testDatabase.Factory),
            authorizationService,
            sessionService);
    }

    private static Patient CreateValidPatient()
    {
        return new Patient
        {
            FirstName = "Jane",
            MiddleName = "Marie",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1998, 5, 15),
            Sex = "Female",
            CivilStatus = "Married",
            ContactNumber = "09171234567",
            Email = "jane@example.com",
            Address = "Test Address",
            EmergencyContactName = "John Doe",
            EmergencyContactNumber = "09179876543"
        };
    }

    private static async Task<Patient> RegisterTestPatientAsync(
        PatientService service)
    {
        return await service.RegisterPatientAsync(
            CreateValidPatient());
    }

    [Fact]
    public async Task UpdatePatientAsync_WhenInformationChanges_SavesPatientAuditAndVersion()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service =
            await CreateAuthenticatedServiceAsync(testDatabase);

        var originalPatient =
            await RegisterTestPatientAsync(service);

        var updatedPatient = new Patient
        {
            Id = originalPatient.Id,
            FirstName = "Jane",
            MiddleName = "Marie",
            LastName = "Doe",
            DateOfBirth = originalPatient.DateOfBirth,
            Sex = originalPatient.Sex,
            CivilStatus = originalPatient.CivilStatus,
            ContactNumber = originalPatient.ContactNumber,
            Email = originalPatient.Email,
            Address = originalPatient.Address,
            EmergencyContactName = originalPatient.EmergencyContactName,
            EmergencyContactNumber = originalPatient.EmergencyContactNumber
        };

        updatedPatient.FirstName = "Janet";
        updatedPatient.ContactNumber = "09991234567";

        // Act
        var result =
            await service.UpdatePatientAsync(updatedPatient);

        // Assert: updated patient
        Assert.Equal(originalPatient.Id, result.Id);
        Assert.Equal("Janet", result.FirstName);
        Assert.Equal("09991234567", result.ContactNumber);
        Assert.NotNull(result.UpdatedAtUtc);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        // Assert: patient persistence
        var savedPatient =
            await verificationContext.Patients
                .SingleAsync(patient =>
                    patient.Id == originalPatient.Id);

        Assert.Equal("Janet", savedPatient.FirstName);
        Assert.Equal("09991234567", savedPatient.ContactNumber);

        // Assert: audit log persistence
        var auditLog =
            await verificationContext.AuditLogs
                .SingleAsync(log =>
                    log.EntityType == "Patient" &&
                    log.EntityId == originalPatient.Id &&
                    log.Action == "Updated");

        Assert.Equal("patient-test-user", auditLog.Username);
        Assert.NotNull(auditLog.StaffId);

        // Assert: version history persistence
        var recordVersion =
            await verificationContext.RecordVersions
                .SingleAsync(version =>
                    version.EntityType == "Patient" &&
                    version.EntityId == originalPatient.Id);

        Assert.Equal("patient-test-user", recordVersion.Username);
        Assert.NotNull(recordVersion.StaffId);

        var previousValues =
            JsonSerializer.Deserialize<JsonElement>(
                recordVersion.PreviousValuesJson);

        var newValues =
            JsonSerializer.Deserialize<JsonElement>(
                recordVersion.NewValuesJson);

        Assert.Equal(
            "Jane",
            previousValues.GetProperty("FirstName").GetString());

        Assert.Equal(
            "Janet",
            newValues.GetProperty("FirstName").GetString());

        Assert.Equal(
            "09171234567",
            previousValues.GetProperty("ContactNumber").GetString());

        Assert.Equal(
            "09991234567",
            newValues.GetProperty("ContactNumber").GetString());
    }

    [Fact]
    public async Task UpdatePatientAsync_WhenNoInformationChanges_DoesNotCreateAuditOrVersion()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service =
            await CreateAuthenticatedServiceAsync(testDatabase);

        var originalPatient =
            await RegisterTestPatientAsync(service);

        // Act: submit the same information without changes.
        var result =
            await service.UpdatePatientAsync(originalPatient);

        // Assert: existing patient is returned.
        Assert.Equal(originalPatient.Id, result.Id);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        // Registration creates one audit entry.
        // An unchanged update must not create another.
        var updateAuditCount =
            await verificationContext.AuditLogs.CountAsync(log =>
                log.EntityType == "Patient" &&
                log.EntityId == originalPatient.Id &&
                log.Action == "Updated");

        Assert.Equal(0, updateAuditCount);

        // No version should be created for an unchanged record.
        var versionCount =
            await verificationContext.RecordVersions.CountAsync(version =>
                version.EntityType == "Patient" &&
                version.EntityId == originalPatient.Id);

        Assert.Equal(0, versionCount);

        var savedPatient =
            await verificationContext.Patients
                .SingleAsync(patient =>
                    patient.Id == originalPatient.Id);

        Assert.Null(savedPatient.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdatePatientAsync_WhenPatientDoesNotExist_ThrowsInvalidOperationException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service =
            await CreateAuthenticatedServiceAsync(testDatabase);

        var patient = CreateValidPatient();
        patient.Id = 999999;

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdatePatientAsync(patient));

        Assert.Contains(
            "patient record could not be found",
            exception.Message);
    }

    [Fact]
    public async Task UpdatePatientAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var sessionService = new SessionService();

        var service = new PatientService(
            new PatientRepository(testDatabase.Factory),
            new AuthorizationService(sessionService),
            sessionService);

        var patient = CreateValidPatient();
        patient.Id = 1;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdatePatientAsync(patient));
    }
}
