
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

public class PregnancyServiceTests
{
    private static async Task<PregnancyService> CreateAuthenticatedServiceAsync(
        TestDatabase testDatabase,
        PregnancyRepository pregnancyRepository,
        PatientRepository patientRepository)
    {
        int staffId;
        string securityStamp;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = "pregnancy-test-user",
                PasswordHash = "test-password-hash",
                Role = StaffRoles.Staff,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.StaffMembers.Add(staff);
            await dbContext.SaveChangesAsync();

            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var sessionService = new SessionService();

        sessionService.StartSession(
            new UserSession(
                staffId,
                "pregnancy-test-user",
                StaffRoles.Staff,
                securityStamp));

        var authorizationService =
            new AuthorizationService(
                sessionService,
                new StaffRepository(testDatabase.Factory));

        return new PregnancyService(
            pregnancyRepository,
            patientRepository,
            authorizationService,
            sessionService);
    }

    [Fact]
    public async Task CreatePregnancyAsync_WhenPatientDoesNotExist_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var pregnancyRepository =
            new PregnancyRepository(testDatabase.Factory);

        var patientRepository =
            new PatientRepository(testDatabase.Factory);

        var service = await CreateAuthenticatedServiceAsync(
            testDatabase,
            pregnancyRepository,
            patientRepository);

        var pregnancy = new Pregnancy
        {
            PatientId = 999999,
            Status = "Current",
            LastMenstrualPeriod = new DateOnly(2026, 1, 1),
            EstimatedDueDate = new DateOnly(2026, 10, 8)
        };

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CreatePregnancyAsync(pregnancy));

        Assert.Contains(
            "selected patient could not be found",
            exception.Message);
    }

    [Fact]
    public async Task CreatePregnancyAsync_WhenPatientExists_SavesPregnancy()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var patientRepository =
            new PatientRepository(testDatabase.Factory);

        var pregnancyRepository =
            new PregnancyRepository(testDatabase.Factory);

        int patientId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-002",
                FirstName = "Jane",
                LastName = "Doe",
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Patients.Add(patient);
            await dbContext.SaveChangesAsync();

            patientId = patient.Id;
        }

        var service = await CreateAuthenticatedServiceAsync(
            testDatabase,
            pregnancyRepository,
            patientRepository);

        var pregnancy = new Pregnancy
        {
            PatientId = patientId,
            Status = "Current",
            LastMenstrualPeriod = new DateOnly(2026, 1, 1),
            EstimatedDueDate = new DateOnly(2026, 10, 8),
            Notes = "Test pregnancy record"
        };

        var createdPregnancy =
            await service.CreatePregnancyAsync(pregnancy);

        Assert.NotNull(createdPregnancy);
        Assert.True(createdPregnancy.Id > 0);
        Assert.Equal(patientId, createdPregnancy.PatientId);

        var savedPregnancy =
            await pregnancyRepository.GetByIdAsync(
                createdPregnancy.Id);

        Assert.NotNull(savedPregnancy);
        Assert.Equal(patientId, savedPregnancy.PatientId);
        Assert.Equal("Current", savedPregnancy.Status);
        Assert.Equal("Test pregnancy record", savedPregnancy.Notes);

        await using var auditContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var auditLog = await auditContext.AuditLogs
            .SingleAsync(log =>
                log.EntityType == "Pregnancy" &&
                log.EntityId == createdPregnancy.Id &&
                log.Action == "Created");

        Assert.Equal("pregnancy-test-user", auditLog.Username);
        Assert.Equal("Created", auditLog.Action);
        Assert.Equal("Pregnancy", auditLog.EntityType);
        Assert.Equal(createdPregnancy.Id, auditLog.EntityId);
        Assert.NotNull(auditLog.StaffId);
    }

    [Fact]
    public async Task UpdatePregnancyAsync_WhenPregnancyExists_SavesChangesAndVersionHistory()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var patientRepository =
            new PatientRepository(testDatabase.Factory);

        var pregnancyRepository =
            new PregnancyRepository(testDatabase.Factory);

        int patientId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-UPDATE-001",
                FirstName = "Jane",
                LastName = "Doe",
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Patients.Add(patient);
            await dbContext.SaveChangesAsync();

            patientId = patient.Id;
        }

        var service = await CreateAuthenticatedServiceAsync(
            testDatabase,
            pregnancyRepository,
            patientRepository);

        var originalPregnancy =
            await service.CreatePregnancyAsync(
                new Pregnancy
                {
                    PatientId = patientId,
                    Status = "Current",
                    LastMenstrualPeriod = new DateOnly(2026, 1, 1),
                    EstimatedDueDate = new DateOnly(2026, 10, 8),
                    Notes = "Original notes"
                });

        var updatedPregnancy = new Pregnancy
        {
            Id = originalPregnancy.Id,
            PatientId = patientId,
            Status = "Completed",
            LastMenstrualPeriod = new DateOnly(2026, 1, 1),
            EstimatedDueDate = new DateOnly(2026, 10, 8),
            PregnancyEndDate = new DateOnly(2026, 9, 20),
            Notes = "Updated pregnancy notes"
        };

        var result =
            await service.UpdatePregnancyAsync(updatedPregnancy);

        Assert.Equal(originalPregnancy.Id, result.Id);
        Assert.Equal(patientId, result.PatientId);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Updated pregnancy notes", result.Notes);
        Assert.NotNull(result.UpdatedAtUtc);

        var savedPregnancy =
            await pregnancyRepository.GetByIdAsync(
                originalPregnancy.Id);

        Assert.NotNull(savedPregnancy);
        Assert.Equal("Completed", savedPregnancy.Status);
        Assert.Equal(
            new DateOnly(2026, 9, 20),
            savedPregnancy.PregnancyEndDate);
        Assert.Equal(
            "Updated pregnancy notes",
            savedPregnancy.Notes);

        await using var auditContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var auditLog = await auditContext.AuditLogs
            .SingleAsync(log =>
                log.EntityType == "Pregnancy" &&
                log.EntityId == originalPregnancy.Id &&
                log.Action == "Updated");

        Assert.Equal("pregnancy-test-user", auditLog.Username);
        Assert.Equal("Updated", auditLog.Action);
        Assert.Equal("Pregnancy", auditLog.EntityType);
        Assert.Equal(originalPregnancy.Id, auditLog.EntityId);
        Assert.NotNull(auditLog.StaffId);

        await using var versionContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var version = await versionContext.RecordVersions
            .SingleAsync(record =>
                record.EntityType == "Pregnancy" &&
                record.EntityId == originalPregnancy.Id);

        Assert.Equal("pregnancy-test-user", version.Username);
        Assert.Equal(originalPregnancy.Id, version.EntityId);
        Assert.Equal("Pregnancy", version.EntityType);
        Assert.NotNull(version.StaffId);

        using var previousDocument =
            JsonDocument.Parse(version.PreviousValuesJson);

        using var newDocument =
            JsonDocument.Parse(version.NewValuesJson);

        var previousValues = previousDocument.RootElement;
        var newValues = newDocument.RootElement;

        Assert.Equal(3, newValues.EnumerateObject().Count());
        Assert.Equal(3, previousValues.EnumerateObject().Count());

        Assert.True(newValues.TryGetProperty(
            nameof(Pregnancy.Status), out var newStatus));

        Assert.Equal("Completed", newStatus.GetString());

        Assert.True(previousValues.TryGetProperty(
            nameof(Pregnancy.Status), out var previousStatus));

        Assert.Equal("Current", previousStatus.GetString());

        Assert.True(newValues.TryGetProperty(
            nameof(Pregnancy.PregnancyEndDate), out var newEndDate));

        Assert.Equal("2026-09-20", newEndDate.GetString());

        Assert.True(previousValues.TryGetProperty(
            nameof(Pregnancy.PregnancyEndDate), out var previousEndDate));

        Assert.Equal(JsonValueKind.Null, previousEndDate.ValueKind);

        Assert.True(newValues.TryGetProperty(
            nameof(Pregnancy.Notes), out var newNotes));

        Assert.Equal("Updated pregnancy notes", newNotes.GetString());

        Assert.True(previousValues.TryGetProperty(
            nameof(Pregnancy.Notes), out var previousNotes));

        Assert.Equal("Original notes", previousNotes.GetString());

        Assert.False(newValues.TryGetProperty(
            nameof(Pregnancy.LastMenstrualPeriod), out _));

        Assert.False(newValues.TryGetProperty(
            nameof(Pregnancy.EstimatedDueDate), out _));
    }

    [Fact]
    public async Task UpdatePregnancyAsync_WhenNoValuesChanged_DoesNotCreateAuditOrVersion()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var patientRepository =
            new PatientRepository(testDatabase.Factory);

        var pregnancyRepository =
            new PregnancyRepository(testDatabase.Factory);

        int patientId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-NOCHANGE-001",
                FirstName = "Jane",
                LastName = "Doe",
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Patients.Add(patient);
            await dbContext.SaveChangesAsync();

            patientId = patient.Id;
        }

        var service = await CreateAuthenticatedServiceAsync(
            testDatabase,
            pregnancyRepository,
            patientRepository);

        var originalPregnancy =
            await service.CreatePregnancyAsync(
                new Pregnancy
                {
                    PatientId = patientId,
                    Status = "Current",
                    LastMenstrualPeriod = new DateOnly(2026, 1, 1),
                    EstimatedDueDate = new DateOnly(2026, 10, 8),
                    Notes = "Original notes"
                });

        var updatedPregnancy = new Pregnancy
        {
            Id = originalPregnancy.Id,
            PatientId = patientId,
            Status = originalPregnancy.Status,
            LastMenstrualPeriod = originalPregnancy.LastMenstrualPeriod,
            EstimatedDueDate = originalPregnancy.EstimatedDueDate,
            PregnancyEndDate = originalPregnancy.PregnancyEndDate,
            Notes = originalPregnancy.Notes
        };

        var result =
            await service.UpdatePregnancyAsync(updatedPregnancy);

        Assert.Equal(originalPregnancy.Id, result.Id);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var updatedAuditCount = await verificationContext.AuditLogs
            .CountAsync(log =>
                log.EntityType == "Pregnancy" &&
                log.EntityId == originalPregnancy.Id &&
                log.Action == "Updated");

        var versionCount = await verificationContext.RecordVersions
            .CountAsync(record =>
                record.EntityType == "Pregnancy" &&
                record.EntityId == originalPregnancy.Id);

        Assert.Equal(0, updatedAuditCount);
        Assert.Equal(0, versionCount);
    }

    [Fact]
    public async Task UpdatePregnancyAsync_WhenPregnancyDoesNotExist_ThrowsInvalidOperationException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var patientRepository =
            new PatientRepository(testDatabase.Factory);

        var pregnancyRepository =
            new PregnancyRepository(testDatabase.Factory);

        var service = await CreateAuthenticatedServiceAsync(
            testDatabase,
            pregnancyRepository,
            patientRepository);

        var pregnancy = new Pregnancy
        {
            Id = 999999,
            PatientId = 1,
            Status = "Current",
            LastMenstrualPeriod = new DateOnly(2026, 1, 1),
            EstimatedDueDate = new DateOnly(2026, 10, 8)
        };

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdatePregnancyAsync(pregnancy));

        Assert.Contains(
            "pregnancy record could not be found",
            exception.Message);
    }

    [Fact]
    public async Task UpdatePregnancyAsync_WhenPatientIdChanges_ThrowsInvalidOperationException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var patientRepository =
            new PatientRepository(testDatabase.Factory);

        var pregnancyRepository =
            new PregnancyRepository(testDatabase.Factory);

        int firstPatientId;
        int secondPatientId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var firstPatient = new Patient
            {
                PatientNumber = "TEST-REASSIGN-001",
                FirstName = "Jane",
                LastName = "Doe",
                CreatedAtUtc = DateTime.UtcNow
            };

            var secondPatient = new Patient
            {
                PatientNumber = "TEST-REASSIGN-002",
                FirstName = "Mary",
                LastName = "Smith",
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Patients.AddRange(
                firstPatient,
                secondPatient);

            await dbContext.SaveChangesAsync();

            firstPatientId = firstPatient.Id;
            secondPatientId = secondPatient.Id;
        }

        var service = await CreateAuthenticatedServiceAsync(
            testDatabase,
            pregnancyRepository,
            patientRepository);

        var originalPregnancy =
            await service.CreatePregnancyAsync(
                new Pregnancy
                {
                    PatientId = firstPatientId,
                    Status = "Current",
                    LastMenstrualPeriod = new DateOnly(2026, 1, 1),
                    EstimatedDueDate = new DateOnly(2026, 10, 8)
                });

        var updatedPregnancy = new Pregnancy
        {
            Id = originalPregnancy.Id,
            PatientId = secondPatientId,
            Status = "Completed",
            LastMenstrualPeriod =
                originalPregnancy.LastMenstrualPeriod,
            EstimatedDueDate =
                originalPregnancy.EstimatedDueDate
        };

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdatePregnancyAsync(updatedPregnancy));

        Assert.Contains(
            "cannot be reassigned to another patient",
            exception.Message);

        var savedPregnancy =
            await pregnancyRepository.GetByIdAsync(
                originalPregnancy.Id);

        Assert.NotNull(savedPregnancy);
        Assert.Equal(firstPatientId, savedPregnancy.PatientId);
        Assert.Equal("Current", savedPregnancy.Status);
    }
}
