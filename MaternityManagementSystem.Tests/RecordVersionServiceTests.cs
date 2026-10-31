using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class RecordVersionServiceTests
{
    [Fact]
    public async Task GetHistoryAsync_WhenAuthenticated_ReturnsRecordHistoryNewestFirst()
    {
        await using var database = await TestDatabase.CreateAsync();

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
                    StaffId = 1,
                    Username = "nurse",
                    ChangedAtUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)
                },
                new RecordVersion
                {
                    EntityType = "Patient",
                    EntityId = 1,
                    PreviousValuesJson = """{"LastName":"Smith"}""",
                    NewValuesJson = """{"LastName":"Santos"}""",
                    StaffId = 1,
                    Username = "nurse",
                    ChangedAtUtc = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc)
                });

            await context.SaveChangesAsync();
        }

        var sessionService = new SessionService();
        sessionService.StartSession(
            new UserSession(1, "nurse", "Nurse/Midwife"));

        var authorizationService =
            new AuthorizationService(sessionService);

        var repository =
            new RecordVersionRepository(database.Factory);

        var service = new RecordVersionService(
            repository,
            authorizationService);

        var history = await service.GetHistoryAsync("Patient", 1);

        Assert.Equal(2, history.Count);
        Assert.Equal("LastName", history[0].PreviousValuesJson.Contains("LastName")
            ? "LastName"
            : string.Empty);
        Assert.True(history[0].ChangedAtUtc > history[1].ChangedAtUtc);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenRecordHasNoHistory_ReturnsEmptyList()
    {
        await using var database = await TestDatabase.CreateAsync();

        var sessionService = new SessionService();
        sessionService.StartSession(
            new UserSession(1, "nurse", "Nurse/Midwife"));

        var authorizationService =
            new AuthorizationService(sessionService);

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
            new AuthorizationService(sessionService);

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
                    StaffId = 1,
                    Username = "nurse",
                    ChangedAtUtc = DateTime.UtcNow
                });

            await context.SaveChangesAsync();
        }

        var sessionService = new SessionService();
        sessionService.StartSession(
            new UserSession(1, "nurse", "Nurse/Midwife"));

        var authorizationService =
            new AuthorizationService(sessionService);

        var repository =
            new RecordVersionRepository(database.Factory);

        var service = new RecordVersionService(
            repository,
            authorizationService);

        var history = await service.GetHistoryAsync("Patient", 2);

        Assert.Empty(history);
    }
}