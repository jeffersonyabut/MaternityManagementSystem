
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public class AuditLogRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public AuditLogRepository()
    {
    }

    public AuditLogRepository(
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

    public async Task AddAsync(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var db = CreateDbContext();

        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count = 100)
    {
        await using var db = CreateDbContext();

        return await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetByEntityAsync(
        string entityType,
        int entityId)
    {
        await using var db = CreateDbContext();

        return await db.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.EntityType == entityType &&
                log.EntityId == entityId)
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .ToListAsync();
    }

    public async Task<AuditLogPage> GetPageAsync(
        AuditLogQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var db = CreateDbContext();

        IQueryable<AuditLog> logs = db.AuditLogs
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string searchTerm = query.SearchTerm.Trim();

            logs = logs.Where(log =>
                log.Username.Contains(searchTerm) ||
                log.Action.Contains(searchTerm) ||
                log.EntityType.Contains(searchTerm) ||
                (log.Details != null &&
                 log.Details.Contains(searchTerm)) ||
                (log.Reason != null &&
                 log.Reason.Contains(searchTerm)));
        }

        if (query.StaffId.HasValue)
        {
            logs = logs.Where(log =>
                log.StaffId == query.StaffId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            string action = query.Action.Trim();

            logs = logs.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            string entityType = query.EntityType.Trim();

            logs = logs.Where(log =>
                log.EntityType == entityType);
        }

        if (query.StartDateUtc.HasValue)
        {
            logs = logs.Where(log =>
                log.OccurredAtUtc >= query.StartDateUtc.Value);
        }

        if (query.EndDateUtc.HasValue)
        {
            logs = logs.Where(log =>
                log.OccurredAtUtc <= query.EndDateUtc.Value);
        }

        int totalCount = await logs.CountAsync();

        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 10);

        List<AuditLog> items = await logs
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new AuditLogPage
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AuditLogSummary> GetSummaryAsync(
    DateTime startOfTodayUtc,
    DateTime startOfTomorrowUtc)
    {
        await using var db = CreateDbContext();

        int totalActivities = await db.AuditLogs
            .CountAsync();

        int todayActivities = await db.AuditLogs
            .CountAsync(log =>
                log.OccurredAtUtc >= startOfTodayUtc &&
                log.OccurredAtUtc < startOfTomorrowUtc);

        AuditLog? latestActivity = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .FirstOrDefaultAsync();

        return new AuditLogSummary
        {
            TotalActivities = totalActivities,
            TodayActivities = todayActivities,
            LatestActivity = latestActivity
        };
    }


    public async Task<List<string>> GetAvailableActionsAsync()
    {
        await using var db = CreateDbContext();

        return await db.AuditLogs
            .AsNoTracking()
            .Select(log => log.Action)
            .Distinct()
            .OrderBy(action => action)
            .ToListAsync();
    }

    public async Task<List<string>> GetAvailableEntityTypesAsync()
    {
        await using var db = CreateDbContext();

        return await db.AuditLogs
            .AsNoTracking()
            .Select(log => log.EntityType)
            .Distinct()
            .OrderBy(entityType => entityType)
            .ToListAsync();
    }


}
