
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Tests;

public class StaffRepositoryTests
{
    private static Staff CreateTestAdmin(string username)
    {
        return new Staff
        {
            Username = username,
            PasswordHash = "test-password-hash",
            Role = StaffRoles.Staff,
            IsActive = false
        };
    }

    [Fact]
    public async Task TryCreateFirstAdminAsync_WhenNoAccountsExist_CreatesActiveAdmin()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var repository =
            new StaffRepository(testDatabase.Factory);

        var admin = CreateTestAdmin("firstadmin");

        bool result =
            await repository.TryCreateFirstAdminAsync(admin);

        Assert.True(result);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAdmin =
            await verificationContext.StaffMembers.SingleAsync();

        Assert.Equal("firstadmin", savedAdmin.Username);
        Assert.Equal("FIRSTADMIN", savedAdmin.NormalizedUsername);
        Assert.Equal(StaffRoles.Admin, savedAdmin.Role);
        Assert.True(savedAdmin.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(savedAdmin.SecurityStamp));
    }

    [Fact]
    public async Task TryCreateFirstAdminAsync_WhenAccountAlreadyExists_ReturnsFalse()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var repository =
            new StaffRepository(testDatabase.Factory);

        var firstAdmin = CreateTestAdmin("firstadmin");

        bool firstResult =
            await repository.TryCreateFirstAdminAsync(firstAdmin);

        Assert.True(firstResult);

        var secondAdmin = CreateTestAdmin("secondadmin");

        bool secondResult =
            await repository.TryCreateFirstAdminAsync(secondAdmin);

        Assert.False(secondResult);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAccounts =
            await verificationContext.StaffMembers.ToListAsync();

        var savedAdmin = Assert.Single(savedAccounts);

        Assert.Equal("firstadmin", savedAdmin.Username);
        Assert.Equal(StaffRoles.Admin, savedAdmin.Role);
    }

    [Fact]
    public async Task TryCreateFirstAdminAsync_WhenCalledConcurrently_CreatesOnlyOneAdmin()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var firstRepository =
            new StaffRepository(testDatabase.Factory);

        var secondRepository =
            new StaffRepository(testDatabase.Factory);

        var firstAdmin = CreateTestAdmin("firstadmin");
        var secondAdmin = CreateTestAdmin("secondadmin");

        var startGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> firstAttempt = Task.Run(async () =>
        {
            await startGate.Task;
            return await firstRepository.TryCreateFirstAdminAsync(firstAdmin);
        });

        Task<bool> secondAttempt = Task.Run(async () =>
        {
            await startGate.Task;
            return await secondRepository.TryCreateFirstAdminAsync(secondAdmin);
        });

        startGate.SetResult();

        bool[] results =
            await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Single(results, result => result);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAccounts =
            await verificationContext.StaffMembers.ToListAsync();

        var savedAdmin = Assert.Single(savedAccounts);

        Assert.Equal(StaffRoles.Admin, savedAdmin.Role);
        Assert.True(savedAdmin.IsActive);

        Assert.Contains(
            savedAdmin.Username,
            new[] { "firstadmin", "secondadmin" });
    }
}
