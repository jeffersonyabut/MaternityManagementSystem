
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class VitalSignRepositoryTests
{
    [Fact]
    public async Task AddAsync_SavesVitalSignAndAuditLog()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var repository = new VitalSignRepository(testDatabase.Factory);

        var vitalSign = CreateValidVitalSign(prenatalVisitId);
        var auditLog = CreateAuditLog("Created");

        var savedVitalSign = await repository.AddAsync(
            vitalSign,
            auditLog);

        Assert.True(savedVitalSign.Id > 0);

        var retrieved = await repository.GetByIdAsync(savedVitalSign.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(120, retrieved.SystolicBloodPressure);
        Assert.Equal(80, retrieved.DiastolicBloodPressure);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAudit = await verificationContext.AuditLogs
            .FirstOrDefaultAsync(log =>
                log.EntityType == "VitalSign" &&
                log.EntityId == savedVitalSign.Id &&
                log.Action == "Created");

        Assert.NotNull(savedAudit);
        Assert.Equal("test-nurse", savedAudit.Username);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesVitalSignAndAuditLog()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var repository = new VitalSignRepository(testDatabase.Factory);

        var vitalSign = await repository.AddAsync(
            CreateValidVitalSign(prenatalVisitId),
            CreateAuditLog("Created"));

        vitalSign.HeartRate = 90;
        vitalSign.Notes = "Updated notes.";
        vitalSign.UpdatedAtUtc = DateTime.UtcNow;

        var updateAudit = CreateAuditLog("Updated");

        await repository.UpdateAsync(vitalSign, updateAudit);

        var updated = await repository.GetByIdAsync(vitalSign.Id);

        Assert.NotNull(updated);
        Assert.Equal(90, updated.HeartRate);
        Assert.Equal("Updated notes.", updated.Notes);
        Assert.NotNull(updated.UpdatedAtUtc);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAudit = await verificationContext.AuditLogs
            .FirstOrDefaultAsync(log =>
                log.EntityType == "VitalSign" &&
                log.EntityId == vitalSign.Id &&
                log.Action == "Updated");

        Assert.NotNull(savedAudit);
        Assert.Equal("test-nurse", savedAudit.Username);
    }

    [Fact]
    public async Task UpdateAsync_WhenRecordIsArchived_ThrowsInvalidOperationException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var repository = new VitalSignRepository(testDatabase.Factory);

        var vitalSign = await repository.AddAsync(
            CreateValidVitalSign(prenatalVisitId),
            CreateAuditLog("Created"));

        await repository.ArchiveAsync(
            vitalSign.Id,
            "Duplicate record.",
            CreateAuditLog("Archived"));

        vitalSign.HeartRate = 95;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.UpdateAsync(
                vitalSign,
                CreateAuditLog("Updated")));

        Assert.Contains(
            "Archived vital signs records cannot be edited.",
            exception.Message);
    }

    [Fact]
    public async Task ArchiveAsync_SavesArchiveAndAuditLog()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var repository = new VitalSignRepository(testDatabase.Factory);

        var vitalSign = await repository.AddAsync(
            CreateValidVitalSign(prenatalVisitId),
            CreateAuditLog("Created"));

        await repository.ArchiveAsync(
            vitalSign.Id,
            "Duplicate record.",
            CreateAuditLog("Archived"));

        var archived = await repository.GetByIdAsync(vitalSign.Id);

        Assert.NotNull(archived);
        Assert.True(archived.IsArchived);
        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.Equal("Duplicate record.", archived.ArchiveReason);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAudit = await verificationContext.AuditLogs
            .FirstOrDefaultAsync(log =>
                log.EntityType == "VitalSign" &&
                log.EntityId == vitalSign.Id &&
                log.Action == "Archived");

        Assert.NotNull(savedAudit);
        Assert.Equal("Duplicate record.", savedAudit.Reason);
    }

    [Fact]
    public async Task GetByPrenatalVisitIdAsync_ExcludesArchivedRecords()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var repository = new VitalSignRepository(testDatabase.Factory);

        var activeVitalSign = await repository.AddAsync(
            CreateValidVitalSign(prenatalVisitId),
            CreateAuditLog("Created"));

        var archivedVitalSign = await repository.AddAsync(
            CreateValidVitalSign(prenatalVisitId),
            CreateAuditLog("Created"));

        await repository.ArchiveAsync(
            archivedVitalSign.Id,
            "Duplicate record.",
            CreateAuditLog("Archived"));

        var results = await repository.GetByPrenatalVisitIdAsync(
            prenatalVisitId);

        Assert.Single(results);
        Assert.Equal(activeVitalSign.Id, results[0].Id);
    }

    private static VitalSign CreateValidVitalSign(int prenatalVisitId)
    {
        return new VitalSign
        {
            PrenatalVisitId = prenatalVisitId,
            SystolicBloodPressure = 120,
            DiastolicBloodPressure = 80,
            HeartRate = 80,
            RespiratoryRate = 18,
            TemperatureCelsius = 36.5m,
            OxygenSaturation = 98m,
            WeightKg = 55m,
            FetalHeartRate = 140,
            RecordedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Notes = "Routine vital signs assessment."
        };
    }

    private static AuditLog CreateAuditLog(string action)
    {
        return new AuditLog
        {
            Username = "test-nurse",
            Action = action,
            EntityType = "VitalSign",
            Details = $"Test vital signs record {action.ToLowerInvariant()}."
        };
    }

    private static async Task<int> CreatePrenatalVisitAsync(
        TestDatabase testDatabase)
    {
        await using var dbContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var patient = new Patient
        {
            PatientNumber = $"TEST-VSR-{Guid.NewGuid():N}",
            FirstName = "Jane",
            LastName = "Doe",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync();

        var pregnancy = new Pregnancy
        {
            PatientId = patient.Id,
            Status = "Current",
            LastMenstrualPeriod = new DateOnly(2026, 1, 1),
            EstimatedDueDate = new DateOnly(2026, 10, 8),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Pregnancies.Add(pregnancy);
        await dbContext.SaveChangesAsync();

        var visit = new PrenatalVisit
        {
            PregnancyId = pregnancy.Id,
            VisitDate = new DateOnly(2026, 2, 1),
            Notes = "Test prenatal visit.",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.PrenatalVisits.Add(visit);
        await dbContext.SaveChangesAsync();

        return visit.Id;
    }
}
