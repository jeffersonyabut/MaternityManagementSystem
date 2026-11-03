
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

public class PregnancyRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public PregnancyRepository()
    {
    }

    public PregnancyRepository(
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

    public async Task<Pregnancy> AddAsync(
        Pregnancy pregnancy,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Pregnancies.Add(pregnancy);

        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.EntityId = pregnancy.Id;
        auditLog.OccurredAtUtc = pregnancy.CreatedAtUtc;

        dbContext.AuditLogs.Add(auditLog);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return pregnancy;
    }

    public async Task<List<Pregnancy>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.Pregnancies
            .AsNoTracking()
            .Where(pregnancy => pregnancy.PatientId == patientId)
            .OrderByDescending(pregnancy => pregnancy.CreatedAtUtc)
            .ThenByDescending(pregnancy => pregnancy.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Pregnancy?> GetByIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        if (pregnancyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancyId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.Pregnancies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                pregnancy => pregnancy.Id == pregnancyId,
                cancellationToken);
    }

    public async Task<List<Pregnancy>> GetForReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    string? searchTerm,
    string? status,
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

        var query = dbContext.Pregnancies
            .AsNoTracking()
            .Include(pregnancy => pregnancy.Patient)
            .AsQueryable();

        // Filter by LMP date.
        if (startDate.HasValue)
        {
            query = query.Where(pregnancy =>
                pregnancy.LastMenstrualPeriod >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(pregnancy =>
                pregnancy.LastMenstrualPeriod <= endDate.Value);
        }

        // Search by patient number or name.
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim();

            query = query.Where(pregnancy =>
                pregnancy.Patient.PatientNumber.Contains(search) ||
                pregnancy.Patient.FirstName.Contains(search) ||
                (pregnancy.Patient.MiddleName != null &&
                 pregnancy.Patient.MiddleName.Contains(search)) ||
                pregnancy.Patient.LastName.Contains(search));
        }

        // Filter by pregnancy status.
        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(
                status,
                "All",
                StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(pregnancy =>
                pregnancy.Status == status);
        }

        return await query
            .OrderByDescending(pregnancy =>
                pregnancy.LastMenstrualPeriod)
            .ThenBy(pregnancy => pregnancy.Patient.LastName)
            .ToListAsync(cancellationToken);
    }


    public async Task UpdateAsync(
        Pregnancy pregnancy,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var existingPregnancy = await dbContext.Pregnancies
            .FirstOrDefaultAsync(
                existing => existing.Id == pregnancy.Id,
                cancellationToken);

        if (existingPregnancy is null)
        {
            throw new KeyNotFoundException(
                $"Pregnancy with ID {pregnancy.Id} was not found.");
        }

        if (existingPregnancy.PatientId != pregnancy.PatientId)
        {
            throw new InvalidOperationException(
                "A pregnancy record cannot be reassigned to another patient.");
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
            nameof(Pregnancy.Status),
            existingPregnancy.Status,
            pregnancy.Status);

        TrackChange(
            nameof(Pregnancy.LastMenstrualPeriod),
            existingPregnancy.LastMenstrualPeriod,
            pregnancy.LastMenstrualPeriod);

        TrackChange(
            nameof(Pregnancy.EstimatedDueDate),
            existingPregnancy.EstimatedDueDate,
            pregnancy.EstimatedDueDate);

        TrackChange(
            nameof(Pregnancy.PregnancyEndDate),
            existingPregnancy.PregnancyEndDate,
            pregnancy.PregnancyEndDate);

        TrackChange(
            nameof(Pregnancy.PregnancyOutcome),
            existingPregnancy.PregnancyOutcome,
            pregnancy.PregnancyOutcome);

        TrackChange(
            nameof(Pregnancy.DeliveryMethod),
            existingPregnancy.DeliveryMethod,
            pregnancy.DeliveryMethod);

        TrackChange(
            nameof(Pregnancy.GestationalAgeAtEndWeeks),
            existingPregnancy.GestationalAgeAtEndWeeks,
            pregnancy.GestationalAgeAtEndWeeks);

        TrackChange(
            nameof(Pregnancy.GestationalAgeAtEndDays),
            existingPregnancy.GestationalAgeAtEndDays,
            pregnancy.GestationalAgeAtEndDays);

        TrackChange(
            nameof(Pregnancy.NumberOfInfants),
            existingPregnancy.NumberOfInfants,
            pregnancy.NumberOfInfants);

        TrackChange(
            nameof(Pregnancy.LivingChildrenCount),
            existingPregnancy.LivingChildrenCount,
            pregnancy.LivingChildrenCount);

        TrackChange(
            nameof(Pregnancy.Complications),
            existingPregnancy.Complications,
            pregnancy.Complications);

        TrackChange(
            nameof(Pregnancy.Notes),
            existingPregnancy.Notes,
            pregnancy.Notes);

        if (newValues.Count == 0)
        {
            return;
        }

        existingPregnancy.Status = pregnancy.Status;
        existingPregnancy.LastMenstrualPeriod =
            pregnancy.LastMenstrualPeriod;
        existingPregnancy.EstimatedDueDate =
            pregnancy.EstimatedDueDate;
        existingPregnancy.PregnancyEndDate =
            pregnancy.PregnancyEndDate;
        existingPregnancy.PregnancyOutcome =
            pregnancy.PregnancyOutcome;
        existingPregnancy.DeliveryMethod =
            pregnancy.DeliveryMethod;
        existingPregnancy.GestationalAgeAtEndWeeks =
            pregnancy.GestationalAgeAtEndWeeks;
        existingPregnancy.GestationalAgeAtEndDays =
            pregnancy.GestationalAgeAtEndDays;
        existingPregnancy.NumberOfInfants =
            pregnancy.NumberOfInfants;
        existingPregnancy.LivingChildrenCount =
            pregnancy.LivingChildrenCount;
        existingPregnancy.Complications =
            pregnancy.Complications;
        existingPregnancy.Notes = pregnancy.Notes;
        existingPregnancy.UpdatedAtUtc = pregnancy.UpdatedAtUtc;

        auditLog.EntityId = existingPregnancy.Id;
        auditLog.OccurredAtUtc =
            existingPregnancy.UpdatedAtUtc ?? DateTime.UtcNow;

        var recordVersion = new RecordVersion
        {
            EntityType = "Pregnancy",
            EntityId = existingPregnancy.Id,
            PreviousValuesJson = JsonSerializer.Serialize(previousValues),
            NewValuesJson = JsonSerializer.Serialize(newValues),
            StaffId = auditLog.StaffId,
            Username = auditLog.Username,
            ChangedAtUtc = auditLog.OccurredAtUtc
        };

        dbContext.RecordVersions.Add(recordVersion);
        dbContext.AuditLogs.Add(auditLog);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
