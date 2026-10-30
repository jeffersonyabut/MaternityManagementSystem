
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public class PrenatalVisitRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public PrenatalVisitRepository()
    {
    }

    public PrenatalVisitRepository(
        IDbContextFactory<ClinicDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    private async Task<ClinicDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            return await _contextFactory.CreateDbContextAsync(
                cancellationToken);
        }

        return new ClinicDbContext();
    }

    public async Task<PrenatalVisit> AddAsync(
        PrenatalVisit prenatalVisit,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisit);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await dbContext.PrenatalVisits.AddAsync(
            prenatalVisit,
            cancellationToken);

        // First save: generate the prenatal visit ID.
        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.EntityId = prenatalVisit.Id;
        auditLog.OccurredAtUtc = prenatalVisit.CreatedAtUtc;

        // Second save: persist the audit record.
        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return prenatalVisit;
    }

    public async Task<List<PrenatalVisit>> GetByPregnancyIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        if (pregnancyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancyId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.PrenatalVisits
            .AsNoTracking()
            .Where(visit => visit.PregnancyId == pregnancyId)
            .OrderByDescending(pv => pv.VisitDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<PrenatalVisit?> GetByIdAsync(
        int visitId,
        CancellationToken cancellationToken = default)
    {
        if (visitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.PrenatalVisits
            .AsNoTracking()
            .FirstOrDefaultAsync(
                visit => visit.Id == visitId,
                cancellationToken);
    }

    public async Task UpdateAsync(
        PrenatalVisit prenatalVisit,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisit);
        ArgumentNullException.ThrowIfNull(auditLog);

        if (prenatalVisit.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prenatalVisit));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var existingVisit = await dbContext.PrenatalVisits
            .FirstOrDefaultAsync(
                visit => visit.Id == prenatalVisit.Id,
                cancellationToken);

        if (existingVisit is null)
        {
            throw new InvalidOperationException(
                "The prenatal visit record could not be found.");
        }

        existingVisit.VisitDate = prenatalVisit.VisitDate;
        existingVisit.Notes = prenatalVisit.Notes;
        existingVisit.UpdatedAtUtc = prenatalVisit.UpdatedAtUtc;

        auditLog.EntityId = existingVisit.Id;
        auditLog.OccurredAtUtc =
            existingVisit.UpdatedAtUtc ?? DateTime.UtcNow;

        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        // Visit changes and audit entry are saved together.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
