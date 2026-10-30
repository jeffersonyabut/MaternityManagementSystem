
using System;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class PrenatalVisitServiceTests
{
    [Fact]
    public async Task CreateVisitAsync_WhenPregnancyDoesNotExist_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var prenatalVisitRepository =
            new PrenatalVisitRepository(testDatabase.Factory);

        var service = await CreateServiceAsync(testDatabase);

        var visit = new PrenatalVisit
        {
            PregnancyId = 999999,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = "Test prenatal visit."
        };

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CreateVisitAsync(visit));

        Assert.Contains(
            "selected pregnancy record could not be found",
            exception.Message);
    }

    [Fact]
    public async Task CreateVisitAsync_WhenPregnancyExists_SavesVisit()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var prenatalVisitRepository =
            new PrenatalVisitRepository(testDatabase.Factory);

        int pregnancyId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-PVS-001",
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

            pregnancyId = pregnancy.Id;
        }

        var service = await CreateServiceAsync(testDatabase);

        var visit = new PrenatalVisit
        {
            PregnancyId = pregnancyId,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = "Routine prenatal visit."
        };

        var createdVisit = await service.CreateVisitAsync(visit);

        Assert.NotNull(createdVisit);
        Assert.True(createdVisit.Id > 0);
        Assert.Equal(pregnancyId, createdVisit.PregnancyId);
        Assert.Equal(new DateOnly(2026, 1, 15), createdVisit.VisitDate);
        Assert.Equal("Routine prenatal visit.", createdVisit.Notes);
        Assert.NotEqual(default, createdVisit.CreatedAtUtc);
        Assert.Null(createdVisit.UpdatedAtUtc);

        var savedVisit =
            await prenatalVisitRepository.GetByIdAsync(createdVisit.Id);

        Assert.NotNull(savedVisit);
        Assert.Equal(pregnancyId, savedVisit.PregnancyId);
        Assert.Equal(new DateOnly(2026, 1, 15), savedVisit.VisitDate);
        Assert.Equal("Routine prenatal visit.", savedVisit.Notes);
    }

    [Fact]
    public async Task GetByPregnancyIdAsync_ReturnsVisitsForSelectedPregnancy()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var prenatalVisitRepository =
            new PrenatalVisitRepository(testDatabase.Factory);

        int pregnancyId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-PVS-002",
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

            pregnancyId = pregnancy.Id;
        }

        var service = await CreateServiceAsync(testDatabase);

        await service.CreateVisitAsync(new PrenatalVisit
        {
            PregnancyId = pregnancyId,
            VisitDate = new DateOnly(2026, 2, 1),
            Notes = "First prenatal visit."
        });

        await service.CreateVisitAsync(new PrenatalVisit
        {
            PregnancyId = pregnancyId,
            VisitDate = new DateOnly(2026, 3, 1),
            Notes = "Second prenatal visit."
        });

        var visits = await service.GetByPregnancyIdAsync(pregnancyId);

        Assert.Equal(2, visits.Count);
        Assert.Equal("Second prenatal visit.", visits[0].Notes);
        Assert.Equal("First prenatal visit.", visits[1].Notes);
        Assert.Equal(new DateOnly(2026, 3, 1), visits[0].VisitDate);
        Assert.Equal(new DateOnly(2026, 2, 1), visits[1].VisitDate);
    }

    [Fact]
    public async Task UpdateVisitAsync_WhenVisitExists_SavesChanges()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var prenatalVisitRepository =
            new PrenatalVisitRepository(testDatabase.Factory);

        int pregnancyId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "TEST-PVS-003",
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

            pregnancyId = pregnancy.Id;
        }

        var service = await CreateServiceAsync(testDatabase);

        var originalVisit = await service.CreateVisitAsync(
            new PrenatalVisit
            {
                PregnancyId = pregnancyId,
                VisitDate = new DateOnly(2026, 2, 1),
                Notes = "Original visit notes."
            });

        var updatedVisit = new PrenatalVisit
        {
            Id = originalVisit.Id,
            PregnancyId = pregnancyId,
            VisitDate = new DateOnly(2026, 2, 5),
            Notes = "Updated prenatal visit notes."
        };

        var result = await service.UpdateVisitAsync(updatedVisit);

        Assert.Equal(originalVisit.Id, result.Id);
        Assert.Equal(pregnancyId, result.PregnancyId);
        Assert.Equal(new DateOnly(2026, 2, 5), result.VisitDate);
        Assert.Equal(
            "Updated prenatal visit notes.",
            result.Notes);
        Assert.NotNull(result.UpdatedAtUtc);

        var savedVisit =
            await prenatalVisitRepository.GetByIdAsync(originalVisit.Id);

        Assert.NotNull(savedVisit);
        Assert.Equal(
            new DateOnly(2026, 2, 5),
            savedVisit.VisitDate);
        Assert.Equal(
            "Updated prenatal visit notes.",
            savedVisit.Notes);
    }

    [Fact]
    public async Task UpdateVisitAsync_WhenVisitDoesNotExist_ThrowsInvalidOperationException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var visit = new PrenatalVisit
        {
            Id = 999999,
            PregnancyId = 1,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = "Updated notes."
        };

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateVisitAsync(visit));

        Assert.Contains(
            "prenatal visit record could not be found",
            exception.Message);
    }

    [Fact]
    public async Task UpdateVisitAsync_WhenPregnancyIdChanges_ThrowsInvalidOperationException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var prenatalVisitRepository =
            new PrenatalVisitRepository(testDatabase.Factory);

        int firstPregnancyId;
        int secondPregnancyId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var firstPatient = new Patient
            {
                PatientNumber = "TEST-PVS-004",
                FirstName = "Jane",
                LastName = "Doe",
                CreatedAtUtc = DateTime.UtcNow
            };

            var secondPatient = new Patient
            {
                PatientNumber = "TEST-PVS-005",
                FirstName = "Mary",
                LastName = "Smith",
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Patients.AddRange(firstPatient, secondPatient);
            await dbContext.SaveChangesAsync();

            var firstPregnancy = new Pregnancy
            {
                PatientId = firstPatient.Id,
                Status = "Current",
                LastMenstrualPeriod = new DateOnly(2026, 1, 1),
                EstimatedDueDate = new DateOnly(2026, 10, 8),
                CreatedAtUtc = DateTime.UtcNow
            };

            var secondPregnancy = new Pregnancy
            {
                PatientId = secondPatient.Id,
                Status = "Current",
                LastMenstrualPeriod = new DateOnly(2026, 2, 1),
                EstimatedDueDate = new DateOnly(2026, 11, 8),
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Pregnancies.AddRange(
                firstPregnancy,
                secondPregnancy);

            await dbContext.SaveChangesAsync();

            firstPregnancyId = firstPregnancy.Id;
            secondPregnancyId = secondPregnancy.Id;
        }

        var service = await CreateServiceAsync(testDatabase);

        var originalVisit = await service.CreateVisitAsync(
            new PrenatalVisit
            {
                PregnancyId = firstPregnancyId,
                VisitDate = new DateOnly(2026, 2, 1),
                Notes = "Original prenatal visit."
            });

        var updatedVisit = new PrenatalVisit
        {
            Id = originalVisit.Id,
            PregnancyId = secondPregnancyId,
            VisitDate = originalVisit.VisitDate,
            Notes = "Attempted reassignment."
        };

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateVisitAsync(updatedVisit));

        Assert.Contains(
            "cannot be reassigned to another pregnancy",
            exception.Message);

        var savedVisit =
            await prenatalVisitRepository.GetByIdAsync(originalVisit.Id);

        Assert.NotNull(savedVisit);
        Assert.Equal(firstPregnancyId, savedVisit.PregnancyId);
        Assert.Equal(
            new DateOnly(2026, 2, 1),
            savedVisit.VisitDate);
        Assert.Equal("Original prenatal visit.", savedVisit.Notes);
    }

    private static async Task<PrenatalVisitService> CreateServiceAsync(
        TestDatabase testDatabase)
    {
        int staffId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = "test-nurse",
                PasswordHash = "test-password-hash",
                Role = "Nurse/Midwife",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Set<Staff>().Add(staff);
            await dbContext.SaveChangesAsync();

            staffId = staff.Id;
        }

        var sessionService = new SessionService();

        sessionService.StartSession(
            new UserSession(
                staffId,
                "test-nurse",
                "Nurse/Midwife"));

        var authorizationService =
            new AuthorizationService(sessionService);

        return new PrenatalVisitService(
            new PrenatalVisitRepository(testDatabase.Factory),
            new PregnancyRepository(testDatabase.Factory),
            authorizationService,
            sessionService);
    }
}
