
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class PregnancyRepositoryTests
{
    [Fact]
    public async Task AddAsync_SavesPregnancy_AndCanRetrieveIt()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        int patientId;

        // Arrange: create a patient in the isolated test database.
        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-001",
                FirstName = "Test",
                LastName = "Patient",
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Patients.Add(patient);
            await dbContext.SaveChangesAsync();

            patientId = patient.Id;
        }

        var repository = new PregnancyRepository(
            testDatabase.Factory);

        var pregnancy = new Pregnancy
        {
            PatientId = patientId,
            Status = "Current",
            LastMenstrualPeriod = new DateOnly(2026, 1, 1),
            EstimatedDueDate = new DateOnly(2026, 10, 8),
            CreatedAtUtc = DateTime.UtcNow
        };

        var auditLog = new AuditLog
        {
            StaffId = null,
            Username = "repository-test-user",
            Action = "Created",
            EntityType = "Pregnancy",
            Details = "Pregnancy record registered.",
            OccurredAtUtc = pregnancy.CreatedAtUtc
        };

        // Act: save and retrieve the pregnancy.
        Pregnancy savedPregnancy =
            await repository.AddAsync(
                pregnancy,
                auditLog);

        Pregnancy? retrievedPregnancy =
            await repository.GetByIdAsync(savedPregnancy.Id);

        // Assert: verify the saved pregnancy.
        Assert.NotNull(retrievedPregnancy);
        Assert.True(savedPregnancy.Id > 0);
        Assert.Equal(patientId, retrievedPregnancy.PatientId);
        Assert.Equal("Current", retrievedPregnancy.Status);

        Assert.Equal(
            new DateOnly(2026, 1, 1),
            retrievedPregnancy.LastMenstrualPeriod);

        Assert.Equal(
            new DateOnly(2026, 10, 8),
            retrievedPregnancy.EstimatedDueDate);

        // Assert: verify the audit log was saved.
        await using var auditContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAuditLog = await auditContext.AuditLogs
            .SingleAsync(log =>
                log.EntityType == "Pregnancy" &&
                log.EntityId == savedPregnancy.Id &&
                log.Action == "Created");

        Assert.Equal(
            "repository-test-user",
            savedAuditLog.Username);

        Assert.Equal("Created", savedAuditLog.Action);
        Assert.Equal("Pregnancy", savedAuditLog.EntityType);
        Assert.Equal(savedPregnancy.Id, savedAuditLog.EntityId);
    }
}
