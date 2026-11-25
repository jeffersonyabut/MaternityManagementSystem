
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class AuthorizationServiceTests
{
    private static async Task<(
        AuthorizationService Authorization,
        SessionService Session,
        int StaffId)> CreateAuthenticatedAuthorizationAsync(
            TestDatabase testDatabase)
    {
        int staffId;
        string securityStamp;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = "authorization-test-user",
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
                "authorization-test-user",
                StaffRoles.Staff,
                securityStamp));

        var authorizationService = new AuthorizationService(
            sessionService,
            new StaffRepository(testDatabase.Factory));

        return (
            authorizationService,
            sessionService,
            staffId);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenSessionIsValid_DoesNotThrow()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var result =
            await CreateAuthenticatedAuthorizationAsync(testDatabase);

        await result.Authorization.EnsureAuthenticatedAsync();

        Assert.True(result.Session.IsAuthenticated);
        Assert.NotNull(result.Session.CurrentUser);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenSecurityStampChanges_InvalidatesSession()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var result =
            await CreateAuthenticatedAuthorizationAsync(testDatabase);

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = await dbContext.StaffMembers
                .SingleAsync(member =>
                    member.Id == result.StaffId);

            staff.SecurityStamp = Guid.NewGuid().ToString("N");

            await dbContext.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => result.Authorization.EnsureAuthenticatedAsync());

        Assert.False(result.Session.IsAuthenticated);
        Assert.Null(result.Session.CurrentUser);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenAccountIsDeactivated_InvalidatesSession()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var result =
            await CreateAuthenticatedAuthorizationAsync(testDatabase);

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = await dbContext.StaffMembers
                .SingleAsync(member =>
                    member.Id == result.StaffId);

            staff.IsActive = false;

            await dbContext.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => result.Authorization.EnsureAuthenticatedAsync());

        Assert.False(result.Session.IsAuthenticated);
        Assert.Null(result.Session.CurrentUser);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenAccountIsDeleted_InvalidatesSession()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var result =
            await CreateAuthenticatedAuthorizationAsync(testDatabase);

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = await dbContext.StaffMembers
                .SingleAsync(member =>
                    member.Id == result.StaffId);

            dbContext.StaffMembers.Remove(staff);

            await dbContext.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => result.Authorization.EnsureAuthenticatedAsync());

        Assert.False(result.Session.IsAuthenticated);
        Assert.Null(result.Session.CurrentUser);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenRoleChanges_InvalidatesSession()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var result =
            await CreateAuthenticatedAuthorizationAsync(testDatabase);

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = await dbContext.StaffMembers
                .SingleAsync(member =>
                    member.Id == result.StaffId);

            staff.Role = StaffRoles.Admin;

            await dbContext.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => result.Authorization.EnsureAuthenticatedAsync());

        Assert.False(result.Session.IsAuthenticated);
        Assert.Null(result.Session.CurrentUser);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenNoSessionExists_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var sessionService = new SessionService();

        var authorizationService = new AuthorizationService(
            sessionService,
            new StaffRepository(testDatabase.Factory));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => authorizationService.EnsureAuthenticatedAsync());

        Assert.False(sessionService.IsAuthenticated);
        Assert.Null(sessionService.CurrentUser);
    }
}
