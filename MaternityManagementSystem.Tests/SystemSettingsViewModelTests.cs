
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Tests;

public class SystemSettingsViewModelTests
{
    private static async Task<(
        SystemSettingsViewModel ViewModel,
        RegionalSettingsService RegionalSettings,
        SessionService Session,
        TestDatabase Database)> CreateViewModelAsync(
            IEnumerable<SystemSetting>? initialSettings = null)
    {
        var database = await TestDatabase.CreateAsync();

        int staffId;
        string securityStamp;

        await using (var dbContext =
            await database.Factory.CreateDbContextAsync())
        {
            var admin = new Staff
            {
                Username = "system-settings-test-admin",
                PasswordHash = "test-password-hash",
                Role = StaffRoles.Admin,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.StaffMembers.Add(admin);
            await dbContext.SaveChangesAsync();

            staffId = admin.Id;
            securityStamp = admin.SecurityStamp;

            if (initialSettings is not null)
            {
                foreach (var setting in initialSettings)
                {
                    setting.UpdatedByStaffId = staffId;
                    setting.UpdatedAtUtc = DateTime.UtcNow;

                    dbContext.SystemSettings.Add(setting);
                }

                await dbContext.SaveChangesAsync();
            }
        }

        var sessionService = new SessionService();

        sessionService.StartSession(
            new UserSession(
                staffId,
                "system-settings-test-admin",
                StaffRoles.Admin,
                securityStamp));

        var staffRepository = new StaffRepository(
            database.Factory);

        var settingRepository = new SystemSettingRepository(
            database.Factory);

        var authorizationService = new AuthorizationService(
            sessionService,
            staffRepository);

        var settingService = new SystemSettingService(
            settingRepository,
            authorizationService,
            sessionService);

        var regionalSettings = new RegionalSettingsService(
            settingRepository);

        var viewModel = new SystemSettingsViewModel(
            settingService,
            regionalSettings);

        return (
            viewModel,
            regionalSettings,
            sessionService,
            database);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenNoSettingsExist_LoadsDefaults()
    {
        var result = await CreateViewModelAsync();

        await using var database = result.Database;

        await result.ViewModel.LoadSettingsAsync();

        Assert.Equal(
            SystemSettingDefaults.Values[SystemSettingKeys.Clinic.Name],
            result.ViewModel.ClinicName);

        Assert.Equal(
            SystemSettingDefaults.Values[SystemSettingKeys.Appointments.DefaultDuration],
            result.ViewModel.DefaultAppointmentDuration);

        Assert.Equal(
            SystemSettingDefaults.Values[SystemSettingKeys.Regional.TimeZone],
            result.ViewModel.TimeZone);

        Assert.Equal(
            SystemSettingDefaults.Values[SystemSettingKeys.Regional.DateFormat],
            result.ViewModel.DateFormat);

        Assert.True(result.ViewModel.AreAppointmentRemindersEnabled);
        Assert.False(result.ViewModel.IsSmsEnabled);

        Assert.Equal(
            "System settings loaded successfully.",
            result.ViewModel.StatusMessage);

        Assert.False(result.ViewModel.IsBusy);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenSettingsExist_LoadsPersistedValues()
    {
        var result = await CreateViewModelAsync(
        [
            new SystemSetting
            {
                Key = SystemSettingKeys.Clinic.Name,
                Value = "Goldenstate Maternity Clinic"
            },
            new SystemSetting
            {
                Key = SystemSettingKeys.Appointments.DefaultDuration,
                Value = "45"
            },
            new SystemSetting
            {
                Key = SystemSettingKeys.Notifications.SmsEnabled,
                Value = "true"
            },
            new SystemSetting
            {
                Key = SystemSettingKeys.Notifications.AppointmentReminders,
                Value = "false"
            }
        ]);

        await using var database = result.Database;

        await result.ViewModel.LoadSettingsAsync();

        Assert.Equal(
            "Goldenstate Maternity Clinic",
            result.ViewModel.ClinicName);

        Assert.Equal(
            "45",
            result.ViewModel.DefaultAppointmentDuration);

        Assert.True(result.ViewModel.IsSmsEnabled);
        Assert.False(result.ViewModel.AreAppointmentRemindersEnabled);

        Assert.False(result.ViewModel.IsBusy);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenUnknownSettingsExist_IgnoresThem()
    {
        var result = await CreateViewModelAsync(
        [
            new SystemSetting
            {
                Key = SystemSettingKeys.Clinic.Name,
                Value = "Test Clinic"
            },
            new SystemSetting
            {
                Key = "Unknown.TestSetting",
                Value = "Should not be loaded"
            }
        ]);

        await using var database = result.Database;

        await result.ViewModel.LoadSettingsAsync();

        Assert.Equal(
            "Test Clinic",
            result.ViewModel.ClinicName);

        Assert.Equal(
            SystemSettingDefaults.Values[SystemSettingKeys.Appointments.DefaultDuration],
            result.ViewModel.DefaultAppointmentDuration);

        Assert.Equal(
            "System settings loaded successfully.",
            result.ViewModel.StatusMessage);

        Assert.False(result.ViewModel.IsBusy);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("241")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task SaveSettingsAsync_WhenDurationIsInvalid_DoesNotSave(
        string invalidDuration)
    {
        var result = await CreateViewModelAsync();

        await using var database = result.Database;

        await result.ViewModel.LoadSettingsAsync();

        result.ViewModel.DefaultAppointmentDuration =
            invalidDuration;

        await result.ViewModel.SaveSettingsAsync();

        Assert.Contains(
            "Default appointment duration must be between 5 and 240 minutes.",
            result.ViewModel.StatusMessage);

        Assert.False(result.ViewModel.IsBusy);

        await using var dbContext =
            await database.Factory.CreateDbContextAsync();

        Assert.Empty(
            await dbContext.SystemSettings.ToListAsync());

        Assert.Empty(
            await dbContext.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenMultipleSettingsChange_SavesThemTogether()
    {
        var result = await CreateViewModelAsync();

        await using var database = result.Database;

        await result.ViewModel.LoadSettingsAsync();

        result.ViewModel.ClinicName = "  Golddie Clinic  ";
        result.ViewModel.ClinicAddress = "  General Santos City  ";
        result.ViewModel.DefaultAppointmentDuration = "45";
        result.ViewModel.IsSmsEnabled = true;

        await result.ViewModel.SaveSettingsAsync();

        Assert.Equal(
            "System settings saved successfully.",
            result.ViewModel.StatusMessage);

        Assert.False(result.ViewModel.IsBusy);

        await using var dbContext =
            await database.Factory.CreateDbContextAsync();

        var settings = await dbContext.SystemSettings
            .ToDictionaryAsync(
                setting => setting.Key,
                setting => setting.Value);

        Assert.Equal(
            "Golddie Clinic",
            settings[SystemSettingKeys.Clinic.Name]);

        Assert.Equal(
            "General Santos City",
            settings[SystemSettingKeys.Clinic.Address]);

        Assert.Equal(
            "45",
            settings[SystemSettingKeys.Appointments.DefaultDuration]);

        Assert.Equal(
            "true",
            settings[SystemSettingKeys.Notifications.SmsEnabled]);

        Assert.Equal(4, settings.Count);

        Assert.Equal(
            4,
            await dbContext.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenNothingChanged_DoesNotCreateAuditLogs()
    {
        var result = await CreateViewModelAsync();

        await using var database = result.Database;

        await result.ViewModel.LoadSettingsAsync();

        await result.ViewModel.SaveSettingsAsync();

        Assert.Equal(
            "System settings saved successfully.",
            result.ViewModel.StatusMessage);

        await using var dbContext =
            await database.Factory.CreateDbContextAsync();

        Assert.Empty(
            await dbContext.SystemSettings.ToListAsync());

        Assert.Empty(
            await dbContext.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenRegionalSettingsChange_RefreshesRegionalSettings()
    {
        var result = await CreateViewModelAsync();

        await using var database = result.Database;

        await result.ViewModel.LoadSettingsAsync();

        result.ViewModel.TimeZone = "Tokyo Standard Time";
        result.ViewModel.DateFormat = "yyyy-MM-dd";

        await result.ViewModel.SaveSettingsAsync();

        Assert.Equal(
            "System settings saved successfully.",
            result.ViewModel.StatusMessage);

        Assert.Equal(
            "Tokyo Standard Time",
            result.RegionalSettings.TimeZoneId);

        Assert.Equal(
            "yyyy-MM-dd",
            result.RegionalSettings.DateFormat);

        Assert.False(result.ViewModel.IsBusy);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenSessionExpires_DoesNotDisplayError()
    {
        var result = await CreateViewModelAsync();

        await using var database = result.Database;

        result.Session.ExpireSession();

        await result.ViewModel.LoadSettingsAsync();

        Assert.Equal(
            string.Empty,
            result.ViewModel.StatusMessage);

        Assert.False(result.ViewModel.IsBusy);
        Assert.False(result.Session.IsAuthenticated);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenSessionExpires_DoesNotSaveChanges()
    {
        var result = await CreateViewModelAsync();

        await using var database = result.Database;

        await result.ViewModel.LoadSettingsAsync();

        result.ViewModel.ClinicName = "Changed Clinic";

        result.Session.ExpireSession();

        await result.ViewModel.SaveSettingsAsync();

        Assert.Equal(
            string.Empty,
            result.ViewModel.StatusMessage);

        Assert.False(result.ViewModel.IsBusy);
        Assert.False(result.Session.IsAuthenticated);

        await using var dbContext =
            await database.Factory.CreateDbContextAsync();

        Assert.Empty(
            await dbContext.SystemSettings.ToListAsync());

        Assert.Empty(
            await dbContext.AuditLogs.ToListAsync());
    }
}
