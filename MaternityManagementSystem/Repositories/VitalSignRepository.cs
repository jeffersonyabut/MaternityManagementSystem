
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

public class VitalSignRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public VitalSignRepository()
    {
    }

    public VitalSignRepository(
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

    public async Task<VitalSign> AddAsync(
        VitalSign vitalSign,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vitalSign);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.VitalSigns.AddAsync(
            vitalSign,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.EntityId = vitalSign.Id;
        auditLog.OccurredAtUtc = DateTime.UtcNow;

        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return vitalSign;
    }

    public async Task<VitalSign?> GetByIdAsync(
        int vitalSignId,
        CancellationToken cancellationToken = default)
    {
        if (vitalSignId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vitalSignId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.VitalSigns
            .AsNoTracking()
            .FirstOrDefaultAsync(
                vitalSign => vitalSign.Id == vitalSignId,
                cancellationToken);
    }

    public async Task<List<VitalSign>> GetByPrenatalVisitIdAsync(
        int prenatalVisitId,
        CancellationToken cancellationToken = default)
    {
        if (prenatalVisitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prenatalVisitId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.VitalSigns
            .AsNoTracking()
            .Where(vs =>
                vs.PrenatalVisitId == prenatalVisitId &&
                !vs.IsArchived)
            .OrderByDescending(vs => vs.RecordedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        VitalSign vitalSign,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vitalSign);
        ArgumentNullException.ThrowIfNull(auditLog);

        if (vitalSign.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vitalSign));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var existingVitalSign = await dbContext.VitalSigns
            .FirstOrDefaultAsync(
                vs => vs.Id == vitalSign.Id,
                cancellationToken);

        if (existingVitalSign is null)
        {
            throw new InvalidOperationException(
                "The vital signs record could not be found.");
        }

        if (existingVitalSign.IsArchived)
        {
            throw new InvalidOperationException(
                "Archived vital signs records cannot be edited.");
        }

        if (existingVitalSign.PrenatalVisitId != vitalSign.PrenatalVisitId)
        {
            throw new InvalidOperationException(
                "A vital signs record cannot be reassigned to another prenatal visit.");
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
            nameof(VitalSign.SystolicBloodPressure),
            existingVitalSign.SystolicBloodPressure,
            vitalSign.SystolicBloodPressure);

        TrackChange(
            nameof(VitalSign.DiastolicBloodPressure),
            existingVitalSign.DiastolicBloodPressure,
            vitalSign.DiastolicBloodPressure);

        TrackChange(
            nameof(VitalSign.HeartRate),
            existingVitalSign.HeartRate,
            vitalSign.HeartRate);

        TrackChange(
            nameof(VitalSign.RespiratoryRate),
            existingVitalSign.RespiratoryRate,
            vitalSign.RespiratoryRate);

        TrackChange(
            nameof(VitalSign.TemperatureCelsius),
            existingVitalSign.TemperatureCelsius,
            vitalSign.TemperatureCelsius);

        TrackChange(
            nameof(VitalSign.OxygenSaturation),
            existingVitalSign.OxygenSaturation,
            vitalSign.OxygenSaturation);

        TrackChange(
            nameof(VitalSign.WeightKg),
            existingVitalSign.WeightKg,
            vitalSign.WeightKg);

        TrackChange(
            nameof(VitalSign.FetalHeartRate),
            existingVitalSign.FetalHeartRate,
            vitalSign.FetalHeartRate);

        TrackChange(
            nameof(VitalSign.RecordedAtUtc),
            existingVitalSign.RecordedAtUtc,
            vitalSign.RecordedAtUtc);

        TrackChange(
            nameof(VitalSign.Notes),
            existingVitalSign.Notes,
            vitalSign.Notes);

        // Nothing changed: do not create audit or version records.
        if (newValues.Count == 0)
        {
            return;
        }

        existingVitalSign.SystolicBloodPressure =
            vitalSign.SystolicBloodPressure;

        existingVitalSign.DiastolicBloodPressure =
            vitalSign.DiastolicBloodPressure;

        existingVitalSign.HeartRate = vitalSign.HeartRate;
        existingVitalSign.RespiratoryRate = vitalSign.RespiratoryRate;
        existingVitalSign.TemperatureCelsius = vitalSign.TemperatureCelsius;
        existingVitalSign.OxygenSaturation = vitalSign.OxygenSaturation;
        existingVitalSign.WeightKg = vitalSign.WeightKg;
        existingVitalSign.FetalHeartRate = vitalSign.FetalHeartRate;
        existingVitalSign.RecordedAtUtc = vitalSign.RecordedAtUtc;
        existingVitalSign.Notes = vitalSign.Notes;
        existingVitalSign.UpdatedAtUtc = vitalSign.UpdatedAtUtc;

        auditLog.EntityId = existingVitalSign.Id;
        auditLog.OccurredAtUtc =
            existingVitalSign.UpdatedAtUtc ?? DateTime.UtcNow;

        var recordVersion = new RecordVersion
        {
            EntityType = "VitalSign",
            EntityId = existingVitalSign.Id,
            PreviousValuesJson = JsonSerializer.Serialize(previousValues),
            NewValuesJson = JsonSerializer.Serialize(newValues),
            StaffId = auditLog.StaffId,
            Username = auditLog.Username,
            ChangedAtUtc = auditLog.OccurredAtUtc
        };

        dbContext.RecordVersions.Add(recordVersion);
        dbContext.AuditLogs.Add(auditLog);

        // Vital-sign update, audit entry, and version history
        // are committed together.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(
        int vitalSignId,
        string? archiveReason,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        if (vitalSignId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vitalSignId));
        }

        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var vitalSign = await dbContext.VitalSigns
            .FirstOrDefaultAsync(
                vs => vs.Id == vitalSignId,
                cancellationToken);

        if (vitalSign is null)
        {
            throw new InvalidOperationException(
                "The vital signs record could not be found.");
        }

        if (vitalSign.IsArchived)
        {
            throw new InvalidOperationException(
                "The vital signs record is already archived.");
        }

        var now = DateTime.UtcNow;

        vitalSign.IsArchived = true;
        vitalSign.ArchivedAtUtc = now;
        vitalSign.ArchiveReason = string.IsNullOrWhiteSpace(archiveReason)
            ? null
            : archiveReason.Trim();

        auditLog.EntityId = vitalSign.Id;
        auditLog.OccurredAtUtc = now;
        auditLog.Reason = vitalSign.ArchiveReason;

        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
