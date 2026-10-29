
using System;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class PregnancyServiceTests
{
    [Fact]
    public async Task CreatePregnancyAsync_WhenPatientDoesNotExist_ThrowsArgumentException()
    {
        // Arrange: create an isolated test database.
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var pregnancyRepository =
            new PregnancyRepository(testDatabase.Factory);

        var patientRepository =
            new PatientRepository(testDatabase.Factory);

        var service = new PregnancyService(
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
        // Arrange
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var patientRepository =
            new PatientRepository(testDatabase.Factory);

        var pregnancyRepository =
            new PregnancyRepository(testDatabase.Factory);

        int patientId;

        // Create a patient in the isolated test database.
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

        var service = new PregnancyService(
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

        // Act
        var createdPregnancy =
            await service.CreatePregnancyAsync(pregnancy);

        // Assert
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
    }



    [Fact]
    public async Task UpdatePregnancyAsync_WhenPregnancyExists_SavesChanges()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var patientRepository = new PatientRepository(testDatabase.Factory);
        var pregnancyRepository = new PregnancyRepository(testDatabase.Factory);

        int patientId;

        // Arrange: create a patient in the test database.
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

        var service = new PregnancyService(
            pregnancyRepository,
            patientRepository);

        // Arrange: create the original pregnancy.
        var originalPregnancy = await service.CreatePregnancyAsync(
            new Pregnancy
            {
                PatientId = patientId,
                Status = "Current",
                LastMenstrualPeriod = new DateOnly(2026, 1, 1),
                EstimatedDueDate = new DateOnly(2026, 10, 8),
                Notes = "Original notes"
            });

        // Act: update the pregnancy.
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

        var result = await service.UpdatePregnancyAsync(updatedPregnancy);

        // Assert: verify the returned result.
        Assert.Equal(originalPregnancy.Id, result.Id);
        Assert.Equal(patientId, result.PatientId);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(
            "Updated pregnancy notes",
            result.Notes);
        Assert.NotNull(result.UpdatedAtUtc);

        // Assert: retrieve it again to verify persistence.
        var savedPregnancy =
            await pregnancyRepository.GetByIdAsync(originalPregnancy.Id);

        Assert.NotNull(savedPregnancy);
        Assert.Equal("Completed", savedPregnancy.Status);
        Assert.Equal(
            new DateOnly(2026, 9, 20),
            savedPregnancy.PregnancyEndDate);
        Assert.Equal(
            "Updated pregnancy notes",
            savedPregnancy.Notes);
    }


    [Fact]
    public async Task UpdatePregnancyAsync_WhenPregnancyDoesNotExist_ThrowsInvalidOperationException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var patientRepository = new PatientRepository(testDatabase.Factory);
        var pregnancyRepository = new PregnancyRepository(testDatabase.Factory);

        var service = new PregnancyService(
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdatePregnancyAsync(pregnancy));

        Assert.Contains(
            "pregnancy record could not be found",
            exception.Message);
    }


    [Fact]
    public async Task UpdatePregnancyAsync_WhenPatientIdChanges_ThrowsInvalidOperationException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var patientRepository = new PatientRepository(testDatabase.Factory);
        var pregnancyRepository = new PregnancyRepository(testDatabase.Factory);

        int firstPatientId;
        int secondPatientId;

        // Arrange: create two patients.
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

            dbContext.Patients.AddRange(firstPatient, secondPatient);
            await dbContext.SaveChangesAsync();

            firstPatientId = firstPatient.Id;
            secondPatientId = secondPatient.Id;
        }

        var service = new PregnancyService(
            pregnancyRepository,
            patientRepository);

        // Arrange: create a pregnancy belonging to the first patient.
        var originalPregnancy = await service.CreatePregnancyAsync(
            new Pregnancy
            {
                PatientId = firstPatientId,
                Status = "Current",
                LastMenstrualPeriod = new DateOnly(2026, 1, 1),
                EstimatedDueDate = new DateOnly(2026, 10, 8)
            });

        // Act: attempt to assign it to the second patient.
        var updatedPregnancy = new Pregnancy
        {
            Id = originalPregnancy.Id,
            PatientId = secondPatientId,
            Status = "Completed",
            LastMenstrualPeriod = originalPregnancy.LastMenstrualPeriod,
            EstimatedDueDate = originalPregnancy.EstimatedDueDate
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdatePregnancyAsync(updatedPregnancy));

        Assert.Contains(
            "cannot be reassigned to another patient",
            exception.Message);

        // Assert: the original patient relationship remains unchanged.
        var savedPregnancy =
            await pregnancyRepository.GetByIdAsync(originalPregnancy.Id);

        Assert.NotNull(savedPregnancy);
        Assert.Equal(firstPatientId, savedPregnancy.PatientId);
        Assert.Equal("Current", savedPregnancy.Status);
    }


}
