
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
        ArgumentException.ThrowIfNullOrWhiteSpace(setting.Key);

        await using var db = CreateDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            SystemSetting? existingSetting =
                await db.SystemSettings.FirstOrDefaultAsync(
                    item => item.Key == setting.Key);

            if (existingSetting is null)
            {
                setting.Id = 0;
                setting.UpdatedAtUtc = DateTime.UtcNow;

                db.SystemSettings.Add(setting);

                await db.SaveChangesAsync();

                auditLog.EntityId = setting.Id;
            }
            else
            {
                existingSetting.Value = setting.Value;
                existingSetting.UpdatedAtUtc = DateTime.UtcNow;
                existingSetting.UpdatedByStaffId =
                    setting.UpdatedByStaffId;

                auditLog.EntityId = existingSetting.Id;
            }

            db.AuditLogs.Add(auditLog);

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
