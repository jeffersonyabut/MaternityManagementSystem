
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.EntityId = prenatalVisit.Id;
        auditLog.OccurredAtUtc = prenatalVisit.CreatedAtUtc;

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
            .OrderByDescending(visit => visit.VisitDate)
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


    public async Task<PrenatalVisit?> GetForReportByIdAsync(
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
            .Include(visit => visit.Pregnancy)
                .ThenInclude(pregnancy => pregnancy.Patient)
            .Include(visit => visit.VitalSigns
                .Where(vitalSign => !vitalSign.IsArchived))
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
            throw new KeyNotFoundException(
                $"Prenatal visit with ID {prenatalVisit.Id} was not found.");
        }

        if (existingVisit.PregnancyId != prenatalVisit.PregnancyId)
        {
            throw new InvalidOperationException(
                "A prenatal visit cannot be reassigned to another pregnancy.");
        }

        var previousValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        void TrackChange(
            string fieldName,
            object? previousValue,
            object? newValue)
        {
            if (Equals(previousValue, newValue))
            {
                return;
            }

            previousValues[fieldName] = previousValue;
            newValues[fieldName] = newValue;
        }

        TrackChange(
            nameof(PrenatalVisit.VisitDate),
            existingVisit.VisitDate,
            prenatalVisit.VisitDate);

        TrackChange(
            nameof(PrenatalVisit.Notes),
            existingVisit.Notes,
            prenatalVisit.Notes);

        // Nothing changed: do not create audit or version records.
        if (newValues.Count == 0)
        {
            return;
        }

        existingVisit.VisitDate = prenatalVisit.VisitDate;
        existingVisit.Notes = prenatalVisit.Notes;
        existingVisit.UpdatedAtUtc = prenatalVisit.UpdatedAtUtc;

        auditLog.EntityId = existingVisit.Id;
        auditLog.OccurredAtUtc =
            existingVisit.UpdatedAtUtc ?? DateTime.UtcNow;

        var recordVersion = new RecordVersion
        {
            EntityType = "PrenatalVisit",
            EntityId = existingVisit.Id,
            PreviousValuesJson = JsonSerializer.Serialize(previousValues),
            NewValuesJson = JsonSerializer.Serialize(newValues),
            StaffId = auditLog.StaffId,
            Username = auditLog.Username,
            ChangedAtUtc = auditLog.OccurredAtUtc
        };

        dbContext.RecordVersions.Add(recordVersion);
        dbContext.AuditLogs.Add(auditLog);

        // Visit update, audit entry, and version history
        // are saved together.
        await dbContext.SaveChangesAsync(cancellationToken);
    }


    public async Task<int> GetPrenatalVisitCountForClinicActivityReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.PrenatalVisits
            .AsNoTracking()
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(visit =>
                visit.VisitDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(visit =>
                visit.VisitDate <= endDate.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<DateOnly>> GetVisitDatesForClinicActivityReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.PrenatalVisits
            .AsNoTracking()
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(visit =>
                visit.VisitDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(visit =>
                visit.VisitDate <= endDate.Value);
        }

        return await query
            .OrderBy(visit => visit.VisitDate)
            .Select(visit => visit.VisitDate)
            .ToListAsync(cancellationToken);
    }


    public async Task<List<int>> GetPatientIdsForClinicActivityReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.PrenatalVisits
            .AsNoTracking()
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(visit =>
                visit.VisitDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(visit =>
                visit.VisitDate <= endDate.Value);
        }

        return await query
            .Select(visit => visit.Pregnancy.PatientId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }



}
