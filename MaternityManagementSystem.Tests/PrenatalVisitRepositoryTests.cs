
using System;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class PrenatalVisitRepositoryTests
{
    [Fact]
    public async Task AddAsync_SavesVisit_AndCanRetrieveIt()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        int pregnancyId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-PV-001",
                FirstName = "Test",
                LastName = "Patient",
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

            pregnancyId = pregnancy.Id;
        }

        var repository = new PrenatalVisitRepository(
            testDatabase.Factory);

        var visit = new PrenatalVisit
        {
            PregnancyId = pregnancyId,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = "Routine prenatal visit.",
            CreatedAtUtc = DateTime.UtcNow
        };

        var auditLog = CreateAuditLog("Created");

        PrenatalVisit savedVisit =
            await repository.AddAsync(visit, auditLog);

        PrenatalVisit? retrievedVisit =
            await repository.GetByIdAsync(savedVisit.Id);

        Assert.NotNull(retrievedVisit);
        Assert.Equal(pregnancyId, retrievedVisit.PregnancyId);
        Assert.Equal(new DateOnly(2026, 1, 15), retrievedVisit.VisitDate);
        Assert.Equal("Routine prenatal visit.", retrievedVisit.Notes);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAuditLog = await verificationContext.AuditLogs
            .FindAsync(auditLog.Id);

        Assert.NotNull(savedAuditLog);
        Assert.Equal("Created", savedAuditLog.Action);
        Assert.Equal("PrenatalVisit", savedAuditLog.EntityType);
        Assert.Equal(savedVisit.Id, savedAuditLog.EntityId);
    }

    [Fact]
    public async Task GetByPregnancyIdAsync_ReturnsVisitsForCorrectPregnancy()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        int pregnancyId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-PV-002",
                FirstName = "Test",
                LastName = "Patient",
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

            pregnancyId = pregnancy.Id;
        }

        var repository = new PrenatalVisitRepository(
            testDatabase.Factory);

        var olderVisit = new PrenatalVisit
        {
            PregnancyId = pregnancyId,
            VisitDate = new DateOnly(2026, 2, 1),
            Notes = "First prenatal visit.",
            CreatedAtUtc = DateTime.UtcNow
        };

        var newerVisit = new PrenatalVisit
        {
            PregnancyId = pregnancyId,
            VisitDate = new DateOnly(2026, 3, 1),
            Notes = "Second prenatal visit.",
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(
            olderVisit,
            CreateAuditLog("Created"));

        await repository.AddAsync(
            newerVisit,
            CreateAuditLog("Created"));

        var visits = await repository.GetByPregnancyIdAsync(
            pregnancyId);

        Assert.Equal(2, visits.Count);
        Assert.Equal("Second prenatal visit.", visits[0].Notes);
        Assert.Equal("First prenatal visit.", visits[1].Notes);
        Assert.Equal(new DateOnly(2026, 3, 1), visits[0].VisitDate);
        Assert.Equal(new DateOnly(2026, 2, 1), visits[1].VisitDate);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingVisit()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        int pregnancyId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-PV-003",
                FirstName = "Test",
                LastName = "Patient",
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

            pregnancyId = pregnancy.Id;
        }

        var repository = new PrenatalVisitRepository(
            testDatabase.Factory);

        var visit = new PrenatalVisit
        {
            PregnancyId = pregnancyId,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = "Original notes.",
            CreatedAtUtc = DateTime.UtcNow
        };

        var savedVisit = await repository.AddAsync(
            visit,
            CreateAuditLog("Created"));

        savedVisit.VisitDate = new DateOnly(2026, 1, 20);
        savedVisit.Notes = "Updated prenatal visit notes.";
        savedVisit.UpdatedAtUtc = DateTime.UtcNow;

        var updateAuditLog = CreateAuditLog("Updated");

        await repository.UpdateAsync(
            savedVisit,
            updateAuditLog);

        var updatedVisit = await repository.GetByIdAsync(
            savedVisit.Id);

        Assert.NotNull(updatedVisit);
        Assert.Equal(
            new DateOnly(2026, 1, 20),
            updatedVisit.VisitDate);
        Assert.Equal(
            "Updated prenatal visit notes.",
            updatedVisit.Notes);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAuditLog = await verificationContext.AuditLogs
            .FindAsync(updateAuditLog.Id);

        Assert.NotNull(savedAuditLog);
        Assert.Equal("Updated", savedAuditLog.Action);
        Assert.Equal("PrenatalVisit", savedAuditLog.EntityType);
        Assert.Equal(savedVisit.Id, savedAuditLog.EntityId);
    }

    private static AuditLog CreateAuditLog(string action)
    {
        return new AuditLog
        {
            Username = "test-nurse",
            Action = action,
            EntityType = "PrenatalVisit",
            Details = $"Test prenatal visit {action.ToLowerInvariant()}."
        };
    }
}
