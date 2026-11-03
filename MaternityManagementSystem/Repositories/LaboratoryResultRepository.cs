
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

public class LaboratoryResultRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public LaboratoryResultRepository()
    {
    }

    public LaboratoryResultRepository(
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

    public async Task<LaboratoryResult> AddAsync(
        LaboratoryResult laboratoryResult,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(laboratoryResult);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await dbContext.LaboratoryResults.AddAsync(
            laboratoryResult,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.EntityId = laboratoryResult.Id;
        auditLog.OccurredAtUtc = laboratoryResult.CreatedAtUtc;

        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return laboratoryResult;
    }


    public async Task<LaboratoryResult?> GetByIdAsync(
        int laboratoryResultId,
        CancellationToken cancellationToken = default)
    {
        if (laboratoryResultId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laboratoryResultId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.LaboratoryResults
            .AsNoTracking()
            .Include(result => result.Patient)
            .Include(result => result.PrenatalVisit)
            .FirstOrDefaultAsync(
                result => result.Id == laboratoryResultId,
                cancellationToken);
    }


    public async Task<List<LaboratoryResult>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.LaboratoryResults
            .AsNoTracking()
            .Where(result =>
                result.PatientId == patientId &&
                !result.IsArchived)
            .OrderByDescending(result => result.TestDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<LaboratoryResult>> GetByPrenatalVisitIdAsync(
        int prenatalVisitId,
        CancellationToken cancellationToken = default)
    {
        if (prenatalVisitId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prenatalVisitId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.LaboratoryResults
            .AsNoTracking()
            .Where(result =>
                result.PrenatalVisitId == prenatalVisitId &&
                !result.IsArchived)
            .OrderByDescending(result => result.TestDate)
            .ToListAsync(cancellationToken);
    }


    public async Task<List<LaboratoryResult>> GetForReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    string? searchTerm,
    string? testName,
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

        var query = dbContext.LaboratoryResults
            .AsNoTracking()
            .Include(result => result.Patient)
            .Where(result => !result.IsArchived);

        if (startDate.HasValue)
        {
            query = query.Where(result =>
                result.TestDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(result =>
                result.TestDate <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim();

            query = query.Where(result =>
                result.Patient.PatientNumber.Contains(search) ||
                result.Patient.FirstName.Contains(search) ||
                (result.Patient.MiddleName != null &&
                 result.Patient.MiddleName.Contains(search)) ||
                result.Patient.LastName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(testName))
        {
            var test = testName.Trim();

            query = query.Where(result =>
                result.TestName.Contains(test));
        }

        return await query
            .OrderByDescending(result => result.TestDate)
            .ThenBy(result => result.Patient.LastName)
            .ToListAsync(cancellationToken);
    }


    public async Task UpdateAsync(
        LaboratoryResult laboratoryResult,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(laboratoryResult);
        ArgumentNullException.ThrowIfNull(auditLog);

        if (laboratoryResult.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laboratoryResult));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var existingResult = await dbContext.LaboratoryResults
            .FirstOrDefaultAsync(
                result => result.Id == laboratoryResult.Id,
                cancellationToken);

        if (existingResult is null)
        {
            throw new InvalidOperationException(
                "The laboratory result could not be found.");
        }

        if (existingResult.IsArchived)
        {
            throw new InvalidOperationException(
                "Archived laboratory records cannot be edited.");
        }

        if (existingResult.PatientId != laboratoryResult.PatientId)
        {
            throw new InvalidOperationException(
                "A laboratory result cannot be reassigned to another patient.");
        }

        if (existingResult.PrenatalVisitId !=
            laboratoryResult.PrenatalVisitId)
        {
            throw new InvalidOperationException(
                "A laboratory result cannot be reassigned to another prenatal visit.");
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
            nameof(LaboratoryResult.TestName),
            existingResult.TestName,
            laboratoryResult.TestName);

        TrackChange(
            nameof(LaboratoryResult.TestDate),
            existingResult.TestDate,
            laboratoryResult.TestDate);

        TrackChange(
            nameof(LaboratoryResult.Result),
            existingResult.Result,
            laboratoryResult.Result);

        TrackChange(
            nameof(LaboratoryResult.Unit),
            existingResult.Unit,
            laboratoryResult.Unit);

        TrackChange(
            nameof(LaboratoryResult.ReferenceRange),
            existingResult.ReferenceRange,
            laboratoryResult.ReferenceRange);

        TrackChange(
            nameof(LaboratoryResult.Notes),
            existingResult.Notes,
            laboratoryResult.Notes);

        // Nothing changed: do not create audit or version records.
        if (newValues.Count == 0)
        {
            return;
        }

        existingResult.TestName = laboratoryResult.TestName;
        existingResult.TestDate = laboratoryResult.TestDate;
        existingResult.Result = laboratoryResult.Result;
        existingResult.Unit = laboratoryResult.Unit;
        existingResult.ReferenceRange = laboratoryResult.ReferenceRange;
        existingResult.Notes = laboratoryResult.Notes;
        existingResult.UpdatedAtUtc = laboratoryResult.UpdatedAtUtc;

        auditLog.EntityId = existingResult.Id;
        auditLog.OccurredAtUtc =
            existingResult.UpdatedAtUtc ?? DateTime.UtcNow;

        var recordVersion = new RecordVersion
        {
            EntityType = "LaboratoryResult",
            EntityId = existingResult.Id,
            PreviousValuesJson = JsonSerializer.Serialize(previousValues),
            NewValuesJson = JsonSerializer.Serialize(newValues),
            StaffId = auditLog.StaffId,
            Username = auditLog.Username,
            ChangedAtUtc = auditLog.OccurredAtUtc
        };

        dbContext.RecordVersions.Add(recordVersion);
        dbContext.AuditLogs.Add(auditLog);

        // Laboratory result update, audit entry, and version history
        // are saved together.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(
        int laboratoryResultId,
        string? archiveReason,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        if (laboratoryResultId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laboratoryResultId));
        }

        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var laboratoryResult = await dbContext.LaboratoryResults
            .FirstOrDefaultAsync(
                result => result.Id == laboratoryResultId,
                cancellationToken);

        if (laboratoryResult is null)
        {
            throw new InvalidOperationException(
                "The laboratory result could not be found.");
        }

        if (laboratoryResult.IsArchived)
        {
            throw new InvalidOperationException(
                "The laboratory result is already archived.");
        }

        var now = DateTime.UtcNow;

        laboratoryResult.IsArchived = true;
        laboratoryResult.ArchivedAtUtc = now;
        laboratoryResult.ArchiveReason =
            string.IsNullOrWhiteSpace(archiveReason)
                ? null
                : archiveReason.Trim();

        auditLog.EntityId = laboratoryResult.Id;
        auditLog.OccurredAtUtc = now;
        auditLog.Reason = laboratoryResult.ArchiveReason;

        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
