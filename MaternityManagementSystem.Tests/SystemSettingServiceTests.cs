
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class SystemSettingServiceTests
{
    private static async Task<SystemSettingService> CreateServiceAsync(
        TestDatabase testDatabase,
        string role = StaffRoles.Admin)
    {
        int staffId;
        string username = "settings-test-user";
        string securityStamp;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = username,
                NormalizedUsername = "SETTINGS-TEST-USER",
                PasswordHash = "test-password-hash",
                Role = role,
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
                username,
                role,
                securityStamp));

        var staffRepository =
            new StaffRepository(testDatabase.Factory);

        var authorizationService = new AuthorizationService(
            sessionService,
            staffRepository);

        var settingRepository =
            new SystemSettingRepository(testDatabase.Factory);

        return new SystemSettingService(
            settingRepository,
            authorizationService,
            sessionService);
    }

    [Fact]
    public async Task SaveBatchAsync_WhenUnknownKeyIsProvided_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var values = new Dictionary<string, string>
        {
            ["Unknown.Setting"] = "value"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SaveBatchAsync(values));
    }

    [Theory]
    [InlineData("4")]
    [InlineData("241")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task SaveBatchAsync_WhenDurationIsInvalid_ThrowsArgumentException(
        string duration)
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Appointments.DefaultDuration] = duration
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SaveBatchAsync(values));
    }

    [Theory]
    [InlineData("5")]
    [InlineData("30")]
    [InlineData("240")]
    public async Task SaveBatchAsync_WhenDurationIsValid_SavesSetting(
        string duration)
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Appointments.DefaultDuration] = duration
        };

        await service.SaveBatchAsync(values);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedSetting =
            await verificationContext.SystemSettings.SingleAsync();

        Assert.Equal(
            SystemSettingKeys.Appointments.DefaultDuration,
            savedSetting.Key);

        Assert.Equal(duration, savedSetting.Value);
    }

    [Fact]
    public async Task SaveBatchAsync_WhenBooleanSettingIsInvalid_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Notifications.SmsEnabled] = "yes"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SaveBatchAsync(values));
    }

    [Fact]
    public async Task SaveBatchAsync_WhenTimeZoneIsInvalid_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Regional.TimeZone] = "Invalid/TimeZone"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SaveBatchAsync(values));
    }

    [Fact]
    public async Task SaveBatchAsync_WhenDateFormatIsInvalid_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Regional.DateFormat] = "%"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SaveBatchAsync(values));
    }

    [Fact]
    public async Task SaveBatchAsync_WhenUserIsStaff_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(
            testDatabase,
            StaffRoles.Staff);

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Clinic.Name] = "Test Clinic"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SaveBatchAsync(values));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Empty(
            await verificationContext.SystemSettings.ToListAsync());
    }

    [Fact]
    public async Task SaveBatchAsync_WhenValuesAreValid_SavesSettingsAndAuditLogs()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Clinic.Name] = "Goldenstate Maternity Clinic",
            [SystemSettingKeys.Clinic.Address] = "General Santos City",
            [SystemSettingKeys.Appointments.DefaultDuration] = "30"
        };

        await service.SaveBatchAsync(values);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedSettings =
            await verificationContext.SystemSettings.ToListAsync();

        var savedAuditLogs =
            await verificationContext.AuditLogs.ToListAsync();

        Assert.Equal(3, savedSettings.Count);
        Assert.Equal(3, savedAuditLogs.Count);

        Assert.Contains(
            savedSettings,
            setting =>
                setting.Key == SystemSettingKeys.Clinic.Name &&
                setting.Value == "Goldenstate Maternity Clinic");

        Assert.All(
            savedAuditLogs,
            auditLog =>
            {
                Assert.Equal("Create", auditLog.Action);
                Assert.Equal("SystemSetting", auditLog.EntityType);
                Assert.Equal("settings-test-user", auditLog.Username);
                Assert.NotNull(auditLog.EntityId);
            });
    }

    [Fact]
    public async Task SaveBatchAsync_WhenAuditLogInsertFails_RollsBackEntireBatch()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER FailAuditLogInsert
                BEFORE INSERT ON AuditLogs
                BEGIN
                    SELECT RAISE(ABORT, 'Forced audit log failure');
                END;
                """);
        }

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Clinic.Name] = "Goldenstate Maternity Clinic",
            [SystemSettingKeys.Clinic.Address] = "General Santos City",
            [SystemSettingKeys.Appointments.DefaultDuration] = "30"
        };

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.SaveBatchAsync(values));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Empty(
            await verificationContext.SystemSettings.ToListAsync());

        Assert.Empty(
            await verificationContext.AuditLogs.ToListAsync());
    }


    [Fact]
    public async Task SaveBatchAsync_WhenAuditLogInsertFailsDuringUpdate_PreservesOriginalSettings()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            dbContext.SystemSettings.AddRange(
                new SystemSetting
                {
                    Key = SystemSettingKeys.Clinic.Name,
                    Value = "Original Clinic"
                },
                new SystemSetting
                {
                    Key = SystemSettingKeys.Clinic.Address,
                    Value = "Original Address"
                });

            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
            CREATE TRIGGER FailAuditLogInsert
            BEFORE INSERT ON AuditLogs
            BEGIN
                SELECT RAISE(ABORT, 'Forced audit log failure');
            END;
            """);
        }

        var values = new Dictionary<string, string>
        {
            [SystemSettingKeys.Clinic.Name] = "Updated Clinic",
            [SystemSettingKeys.Clinic.Address] = "Updated Address"
        };

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.SaveBatchAsync(values));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedSettings =
            await verificationContext.SystemSettings
                .ToDictionaryAsync(
                    setting => setting.Key,
                    setting => setting.Value);

        Assert.Equal(2, savedSettings.Count);

        Assert.Equal(
            "Original Clinic",
            savedSettings[SystemSettingKeys.Clinic.Name]);

        Assert.Equal(
            "Original Address",
            savedSettings[SystemSettingKeys.Clinic.Address]);

        Assert.Empty(
            await verificationContext.AuditLogs.ToListAsync());
    }


}
