
using System;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class RecordVersionServiceTests
{
    private static async Task<(SessionService SessionService, int StaffId)>
        CreateAuthenticatedSessionAsync(TestDatabase database)
    {
        int staffId;
        string securityStamp;

        await using (var context =
            await database.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = "test-staff",
                PasswordHash = "test-password-hash",
                Role = StaffRoles.Staff,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            context.StaffMembers.Add(staff);
            await context.SaveChangesAsync();

            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var sessionService = new SessionService();

        sessionService.StartSession(
            new UserSession(
                staffId,
                "test-staff",
                StaffRoles.Staff,
                securityStamp));

        return (sessionService, staffId);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenAuthenticated_ReturnsRecordHistoryNewestFirst()
    {
        await using var database = await TestDatabase.CreateAsync();

        var (sessionService, staffId) =
            await CreateAuthenticatedSessionAsync(database);

        await using (var context =
            await database.Factory.CreateDbContextAsync())
        {
            context.RecordVersions.AddRange(
                new RecordVersion
                {
                    EntityType = "Patient",
                    EntityId = 1,
                    PreviousValuesJson = """{"FirstName":"John"}""",
                    NewValuesJson = """{"FirstName":"Jonathan"}""",
                    StaffId = staffId,
                    Username = "test-staff",
                    ChangedAtUtc = new DateTime(
                        2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)
                },
                new RecordVersion
                {
                    EntityType = "Patient",
                    EntityId = 1,
                    PreviousValuesJson = """{"LastName":"Smith"}""",
                    NewValuesJson = """{"LastName":"Santos"}""",
                    StaffId = staffId,
                    Username = "test-staff",
                    ChangedAtUtc = new DateTime(
                        2026, 1, 2, 8, 0, 0, DateTimeKind.Utc)
                });

            await context.SaveChangesAsync();
        }

        var authorizationService =
            new AuthorizationService(
                sessionService,
                new StaffRepository(database.Factory));

        var repository =
            new RecordVersionRepository(database.Factory);

        var service = new RecordVersionService(
            repository,
            authorizationService);

        var history = await service.GetHistoryAsync("Patient", 1);

        Assert.Equal(2, history.Count);
        Assert.Contains("LastName", history[0].PreviousValuesJson);
        Assert.True(history[0].ChangedAtUtc > history[1].ChangedAtUtc);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenRecordHasNoHistory_ReturnsEmptyList()
    {
        await using var database = await TestDatabase.CreateAsync();

        var (sessionService, _) =
            await CreateAuthenticatedSessionAsync(database);

        var authorizationService =
            new AuthorizationService(
                sessionService,
                new StaffRepository(database.Factory));

        var repository =
            new RecordVersionRepository(database.Factory);

        var service = new RecordVersionService(
            repository,
            authorizationService);

        var history = await service.GetHistoryAsync("Patient", 999);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var database = await TestDatabase.CreateAsync();

        var sessionService = new SessionService();

        var authorizationService =
            new AuthorizationService(
                sessionService,
                new StaffRepository(database.Factory));

        var repository =
            new RecordVersionRepository(database.Factory);

        var service = new RecordVersionService(
            repository,
            authorizationService);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetHistoryAsync("Patient", 1));
    }

    [Fact]
    public async Task GetHistoryAsync_WhenDifferentRecordRequested_DoesNotReturnOtherRecordHistory()
    {
        await using var database = await TestDatabase.CreateAsync();

        var (sessionService, staffId) =
            await CreateAuthenticatedSessionAsync(database);

        await using (var context =
            await database.Factory.CreateDbContextAsync())
        {
            context.RecordVersions.Add(
                new RecordVersion
                {
                    EntityType = "Patient",
                    EntityId = 1,
                    PreviousValuesJson = """{"FirstName":"John"}""",
                    NewValuesJson = """{"FirstName":"James"}""",
                    StaffId = staffId,
                    Username = "test-staff",
                    ChangedAtUtc = DateTime.UtcNow
                });

            await context.SaveChangesAsync();
        }

        var authorizationService =
            new AuthorizationService(
                sessionService,
                new StaffRepository(database.Factory));

        var repository =
            new RecordVersionRepository(database.Factory);

        var service = new RecordVersionService(
            repository,
            authorizationService);

        var history = await service.GetHistoryAsync("Patient", 2);

        Assert.Empty(history);
    }
}
