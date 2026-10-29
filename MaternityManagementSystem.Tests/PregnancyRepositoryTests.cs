
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

        // Act: save and retrieve the pregnancy.
        Pregnancy savedPregnancy =
            await repository.AddAsync(pregnancy);

        Pregnancy? retrievedPregnancy =
            await repository.GetByIdAsync(savedPregnancy.Id);

        // Assert: verify the saved data.
        Assert.NotNull(retrievedPregnancy);
        Assert.Equal(patientId, retrievedPregnancy.PatientId);
        Assert.Equal("Current", retrievedPregnancy.Status);
        Assert.Equal(
            new DateOnly(2026, 1, 1),
            retrievedPregnancy.LastMenstrualPeriod);
        Assert.Equal(
            new DateOnly(2026, 10, 8),
            retrievedPregnancy.EstimatedDueDate);
    }
}
