
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public sealed class SystemSettingInitializer
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public SystemSettingInitializer()
    {
    }

    public SystemSettingInitializer(
        IDbContextFactory<ClinicDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    private ClinicDbContext CreateDbContext()
    {
        return _contextFactory?.CreateDbContext()
            ?? new ClinicDbContext();
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();

        var existingKeys = await db.SystemSettings
            .Select(setting => setting.Key)
            .ToListAsync();

        var existingKeySet = existingKeys.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var newSettings = SystemSettingDefaults.Values
            .Where(setting => !existingKeySet.Contains(setting.Key))
            .Select(setting => new SystemSetting
            {
                Key = setting.Key,
                Value = setting.Value,
                UpdatedAtUtc = DateTime.UtcNow
            })
            .ToList();

        if (newSettings.Count == 0)
        {
            return;
        }

        await db.SystemSettings.AddRangeAsync(newSettings);

        await db.SaveChangesAsync();
    }
}
