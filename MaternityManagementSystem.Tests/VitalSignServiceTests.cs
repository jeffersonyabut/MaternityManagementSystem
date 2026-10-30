
using System;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class VitalSignServiceTests
{
    [Fact]
    public async Task CreateVitalSignAsync_WhenAuthenticated_SavesVitalSignAndAudit()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);

        var vitalSign = CreateValidVitalSign(prenatalVisitId);

        var result = await service.CreateVitalSignAsync(vitalSign);

        Assert.True(result.Id > 0);
        Assert.Equal(prenatalVisitId, result.PrenatalVisitId);
        Assert.Equal(120, result.SystolicBloodPressure);
        Assert.Equal(80, result.DiastolicBloodPressure);
        Assert.NotEqual(default, result.CreatedAtUtc);
        Assert.Null(result.UpdatedAtUtc);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var auditLog =
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(
                    verificationContext.AuditLogs,
                    log => log.EntityType == "VitalSign" &&
                           log.EntityId == result.Id &&
                           log.Action == "Created");

        Assert.NotNull(auditLog);
        Assert.Equal("test-nurse", auditLog.Username);
    }

    [Fact]
    public async Task CreateVitalSignAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var service = CreateUnauthenticatedService(testDatabase);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateVitalSignAsync(
                CreateValidVitalSign(prenatalVisitId)));

        Assert.Contains("logged in", exception.Message);
    }

    [Fact]
    public async Task CreateVitalSignAsync_WhenPrenatalVisitDoesNotExist_ThrowsArgumentException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateVitalSignAsync(
                CreateValidVitalSign(999999)));

        Assert.Contains("could not be found", exception.Message);
    }

    [Fact]
    public async Task CreateVitalSignAsync_WhenBloodPressureIsIncomplete_ThrowsArgumentException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);

        var vitalSign = CreateValidVitalSign(prenatalVisitId);
        vitalSign.DiastolicBloodPressure = null;

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateVitalSignAsync(vitalSign));
    }

    [Fact]
    public async Task GetByIdAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var service = CreateUnauthenticatedService(testDatabase);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetByIdAsync(1));
    }

    [Fact]
    public async Task GetByPrenatalVisitIdAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var service = CreateUnauthenticatedService(testDatabase);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetByPrenatalVisitIdAsync(1));
    }

    [Fact]
    public async Task UpdateVitalSignAsync_WhenAuthenticated_UpdatesVitalSignAndAudit()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);

        var original = await service.CreateVitalSignAsync(
            CreateValidVitalSign(prenatalVisitId));

        var updated = CreateValidVitalSign(prenatalVisitId);
        updated.Id = original.Id;
        updated.HeartRate = 90;
        updated.Notes = "Updated vital signs notes.";

        var result = await service.UpdateVitalSignAsync(updated);

        Assert.Equal(original.Id, result.Id);
        Assert.Equal(90, result.HeartRate);
        Assert.Equal("Updated vital signs notes.", result.Notes);
        Assert.NotNull(result.UpdatedAtUtc);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var auditLog =
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(
                    verificationContext.AuditLogs,
                    log => log.EntityType == "VitalSign" &&
                           log.EntityId == original.Id &&
                           log.Action == "Updated");

        Assert.NotNull(auditLog);
        Assert.Equal("test-nurse", auditLog.Username);
    }

    [Fact]
    public async Task UpdateVitalSignAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var service = CreateUnauthenticatedService(testDatabase);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdateVitalSignAsync(
                new VitalSign { Id = 1 }));
    }

    [Fact]
    public async Task UpdateVitalSignAsync_WhenRecordIsArchived_ThrowsInvalidOperationException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var prenatalVisitId = await CreatePrenatalVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);

        var original = await service.CreateVitalSignAsync(
            CreateValidVitalSign(prenatalVisitId));

        await service.ArchiveVitalSignAsync(
            original.Id,
            "Duplicate record.");

        var updated = CreateValidVitalSign(prenatalVisitId);
        updated.Id = original.Id;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateVitalSignAsync(updated));

        Assert.Contains(
            "Archived vital signs records cannot be edited.",
            exception.Message);
    }

    [Fact]
    public async Task ArchiveVitalSignAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();

        var service = CreateUnauthenticatedService(testDatabase);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ArchiveVitalSignAsync(1, "Test reason."));
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
            Notes = "Routine vital signs assessment."
        };
    }

    private static async Task<int> CreatePrenatalVisitAsync(
        TestDatabase testDatabase)
    {
        await using var dbContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var patient = new Patient
        {
            PatientNumber = $"TEST-VS-{Guid.NewGuid():N}",
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

    private static async Task<VitalSignService> CreateServiceAsync(
        TestDatabase testDatabase)
    {
        var sessionService = await CreateAuthenticatedSessionAsync(testDatabase);

        return new VitalSignService(
            new VitalSignRepository(testDatabase.Factory),
            new PrenatalVisitRepository(testDatabase.Factory),
            new AuthorizationService(sessionService),
            sessionService);
    }

    private static VitalSignService CreateUnauthenticatedService(
        TestDatabase testDatabase)
    {
        var sessionService = new SessionService();

        return new VitalSignService(
            new VitalSignRepository(testDatabase.Factory),
            new PrenatalVisitRepository(testDatabase.Factory),
            new AuthorizationService(sessionService),
            sessionService);
    }

    private static async Task<SessionService> CreateAuthenticatedSessionAsync(
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

        return sessionService;
    }
}
