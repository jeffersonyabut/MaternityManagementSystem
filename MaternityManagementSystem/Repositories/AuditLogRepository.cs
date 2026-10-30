
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public class AuditLogRepository
{
    public async Task AddAsync(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var db = new ClinicDbContext();

        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count = 100)
    {
        await using var db = new ClinicDbContext();

        return await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAtUtc)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetByEntityAsync(
        string entityType,
        int entityId)
    {
        await using var db = new ClinicDbContext();

        return await db.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.EntityType == entityType &&
                log.EntityId == entityId)
            .OrderByDescending(log => log.OccurredAtUtc)
            .ToListAsync();
    }
}
