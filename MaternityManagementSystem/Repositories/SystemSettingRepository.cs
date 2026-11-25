
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public class SystemSettingRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public SystemSettingRepository()
    {
    }

    public SystemSettingRepository(
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

    public async Task<SystemSetting?> GetByKeyAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var db = CreateDbContext();

        return await db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.Key == key);
    }

    public async Task<List<SystemSetting>> GetAllAsync()
    {
        await using var db = CreateDbContext();

        return await db.SystemSettings
            .AsNoTracking()
            .OrderBy(setting => setting.Key)
            .ToListAsync();
    }

    public async Task UpsertWithAuditAsync(
        SystemSetting setting,
        AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(auditLog);

        await UpsertManyWithAuditAsync(
        [
            (setting, auditLog)
        ]);
    }

    public async Task UpsertManyWithAuditAsync(
        IReadOnlyCollection<(SystemSetting Setting, AuditLog AuditLog)> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return;
        }

        var settingKeys = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var (setting, auditLog) in items)
        {
            ArgumentNullException.ThrowIfNull(setting);
            ArgumentNullException.ThrowIfNull(auditLog);

            ArgumentException.ThrowIfNullOrWhiteSpace(setting.Key);

            if (!settingKeys.Add(setting.Key))
            {
                throw new ArgumentException(
                    $"Duplicate setting key '{setting.Key}' in the batch.",
                    nameof(items));
            }
        }

        await using var db = CreateDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            var auditLogs = new List<AuditLog>();

            foreach (var (setting, auditLog) in items)
            {
                SystemSetting? existingSetting =
                    await db.SystemSettings.FirstOrDefaultAsync(
                        item => item.Key == setting.Key);

                if (existingSetting is null)
                {
                    setting.Id = 0;
                    setting.UpdatedAtUtc = DateTime.UtcNow;

                    db.SystemSettings.Add(setting);

                    auditLog.EntityId = null;
                }
                else
                {
                    existingSetting.Value = setting.Value;
                    existingSetting.UpdatedAtUtc = DateTime.UtcNow;
                    existingSetting.UpdatedByStaffId =
                        setting.UpdatedByStaffId;

                    auditLog.EntityId = existingSetting.Id;
                }

                auditLogs.Add(auditLog);
            }

            // Save settings first so newly created records receive IDs.
            await db.SaveChangesAsync();

            foreach (var (setting, auditLog) in items)
            {
                if (auditLog.EntityId is null)
                {
                    auditLog.EntityId = setting.Id;
                }
            }

            db.AuditLogs.AddRange(auditLogs);

            // Save audit logs before committing the transaction.
            await db.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
