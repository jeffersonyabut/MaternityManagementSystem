
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

public class AppointmentRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public AppointmentRepository()
    {
    }

    public AppointmentRepository(
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

    // CREATE

    public async Task<Appointment> AddAsync(
        Appointment appointment,
        AuditLog auditLog,
        int defaultAppointmentDurationMinutes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        ArgumentNullException.ThrowIfNull(auditLog);

        ValidateDefaultDuration(defaultAppointmentDurationMinutes);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await EnsureNoScheduleConflictAsync(
            dbContext,
            appointment,
            defaultAppointmentDurationMinutes,
            cancellationToken);

        await dbContext.Appointments.AddAsync(
            appointment,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.EntityId = appointment.Id;
        auditLog.OccurredAtUtc = appointment.CreatedAtUtc;

        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return appointment;
    }

    // READ

    public async Task<Appointment?> GetByIdAsync(
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        if (appointmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appointmentId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Pregnancy)
            .FirstOrDefaultAsync(
                appointment => appointment.Id == appointmentId,
                cancellationToken);
    }

    public async Task<List<Appointment>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Pregnancy)
            .Where(appointment => !appointment.IsArchived)
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Appointment>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Pregnancy)
            .Where(appointment =>
                appointment.PatientId == patientId &&
                !appointment.IsArchived)
            .OrderByDescending(appointment => appointment.AppointmentDate)
            .ThenByDescending(appointment => appointment.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Appointment>> GetByPregnancyIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        if (pregnancyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancyId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.PregnancyId == pregnancyId &&
                !appointment.IsArchived)
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Appointment>> GetByDateRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate == default || endDate == default)
        {
            throw new ArgumentException(
                "Valid appointment date range is required.");
        }

        if (endDate < startDate)
        {
            throw new ArgumentException(
                "End date cannot be earlier than start date.");
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Pregnancy)
            .Where(appointment =>
                appointment.AppointmentDate >= startDate &&
                appointment.AppointmentDate <= endDate &&
                !appointment.IsArchived)
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Appointment>> GetForReportAsync(
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

        var query = dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Pregnancy)
            .Where(appointment => !appointment.IsArchived);

        if (startDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim();

            query = query.Where(appointment =>
                appointment.Patient.PatientNumber.Contains(search) ||
                appointment.Patient.FirstName.Contains(search) ||
                (appointment.Patient.MiddleName != null &&
                 appointment.Patient.MiddleName.Contains(search)) ||
                appointment.Patient.LastName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(
                status,
                "All",
                StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(appointment =>
                appointment.Status == status);
        }

        return await query
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.StartTime)
            .ThenBy(appointment => appointment.Patient.LastName)
            .ToListAsync(cancellationToken);
    }

    // UPDATE

    public async Task UpdateAsync(
        Appointment appointment,
        AuditLog auditLog,
        int defaultAppointmentDurationMinutes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        ArgumentNullException.ThrowIfNull(auditLog);

        if (appointment.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appointment));
        }

        ValidateDefaultDuration(defaultAppointmentDurationMinutes);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var existingAppointment = await dbContext.Appointments
            .FirstOrDefaultAsync(
                item => item.Id == appointment.Id,
                cancellationToken);

        if (existingAppointment is null)
        {
            throw new InvalidOperationException(
                "The appointment could not be found.");
        }

        if (existingAppointment.IsArchived)
        {
            throw new InvalidOperationException(
                "Archived appointments cannot be edited.");
        }

        if (existingAppointment.PatientId != appointment.PatientId)
        {
            throw new InvalidOperationException(
                "An appointment cannot be reassigned to another patient.");
        }

        if (existingAppointment.PregnancyId != appointment.PregnancyId)
        {
            throw new InvalidOperationException(
                "An appointment cannot be reassigned to another pregnancy.");
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
            nameof(Appointment.AppointmentDate),
            existingAppointment.AppointmentDate,
            appointment.AppointmentDate);

        TrackChange(
            nameof(Appointment.StartTime),
            existingAppointment.StartTime,
            appointment.StartTime);

        TrackChange(
            nameof(Appointment.EndTime),
            existingAppointment.EndTime,
            appointment.EndTime);

        TrackChange(
            nameof(Appointment.Purpose),
            existingAppointment.Purpose,
            appointment.Purpose);

        TrackChange(
            nameof(Appointment.Notes),
            existingAppointment.Notes,
            appointment.Notes);

        TrackChange(
            nameof(Appointment.Status),
            existingAppointment.Status,
            appointment.Status);

        TrackChange(
            nameof(Appointment.CancellationReason),
            existingAppointment.CancellationReason,
            appointment.CancellationReason);

        if (newValues.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (appointment.Status is "Scheduled" or "Confirmed")
        {
            await EnsureNoScheduleConflictAsync(
                dbContext,
                appointment,
                defaultAppointmentDurationMinutes,
                cancellationToken);
        }

        existingAppointment.AppointmentDate =
            appointment.AppointmentDate;

        existingAppointment.StartTime =
            appointment.StartTime;

        existingAppointment.EndTime =
            appointment.EndTime;

        existingAppointment.Purpose =
            appointment.Purpose;

        existingAppointment.Notes =
            appointment.Notes;

        existingAppointment.Status =
            appointment.Status;

        existingAppointment.CancellationReason =
            appointment.CancellationReason;

        existingAppointment.UpdatedAtUtc =
            appointment.UpdatedAtUtc;

        auditLog.EntityId = existingAppointment.Id;
        auditLog.OccurredAtUtc =
            existingAppointment.UpdatedAtUtc ?? DateTime.UtcNow;

        var recordVersion = new RecordVersion
        {
            EntityType = "Appointment",
            EntityId = existingAppointment.Id,
            PreviousValuesJson = JsonSerializer.Serialize(previousValues),
            NewValuesJson = JsonSerializer.Serialize(newValues),
            StaffId = auditLog.StaffId,
            Username = auditLog.Username,
            ChangedAtUtc = auditLog.OccurredAtUtc
        };

        dbContext.RecordVersions.Add(recordVersion);
        dbContext.AuditLogs.Add(auditLog);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    // ARCHIVE

    public async Task ArchiveAsync(
        int appointmentId,
        string? archiveReason,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        if (appointmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appointmentId));
        }

        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var appointment = await dbContext.Appointments
            .FirstOrDefaultAsync(
                item => item.Id == appointmentId,
                cancellationToken);

        if (appointment is null)
        {
            throw new InvalidOperationException(
                "The appointment could not be found.");
        }

        if (appointment.IsArchived)
        {
            throw new InvalidOperationException(
                "The appointment is already archived.");
        }

        var now = DateTime.UtcNow;

        appointment.IsArchived = true;
        appointment.ArchivedAtUtc = now;
        appointment.ArchiveReason =
            string.IsNullOrWhiteSpace(archiveReason)
                ? null
                : archiveReason.Trim();

        auditLog.EntityId = appointment.Id;
        auditLog.OccurredAtUtc = now;
        auditLog.Reason = appointment.ArchiveReason;

        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    // CLINIC ACTIVITY REPORT

    public async Task<int> GetAppointmentCountForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        ValidateReportDateRange(startDate, endDate);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => !appointment.IsArchived);

        if (startDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate <= endDate.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<DateOnly>> GetAppointmentDatesForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        ValidateReportDateRange(startDate, endDate);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => !appointment.IsArchived);

        if (startDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate <= endDate.Value);
        }

        return await query
            .OrderBy(appointment => appointment.AppointmentDate)
            .Select(appointment => appointment.AppointmentDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<int>> GetPatientIdsForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        ValidateReportDateRange(startDate, endDate);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => !appointment.IsArchived);

        if (startDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate <= endDate.Value);
        }

        return await query
            .Select(appointment => appointment.PatientId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> GetStatusesForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        ValidateReportDateRange(startDate, endDate);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => !appointment.IsArchived);

        if (startDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(appointment =>
                appointment.AppointmentDate <= endDate.Value);
        }

        return await query
            .Select(appointment => appointment.Status)
            .ToListAsync(cancellationToken);
    }

    // TRANSACTION-SAFE CONFLICT VALIDATION

    private static async Task EnsureNoScheduleConflictAsync(
        ClinicDbContext dbContext,
        Appointment proposedAppointment,
        int defaultAppointmentDurationMinutes,
        CancellationToken cancellationToken)
    {
        if (proposedAppointment.Status is not ("Scheduled" or "Confirmed"))
        {
            return;
        }

        if (proposedAppointment.EndTime is null)
        {
            throw new InvalidOperationException(
                "Appointment end time must be calculated before checking conflicts.");
        }

        var existingAppointments = await dbContext.Appointments
            .AsNoTracking()
            .Where(existing =>
                existing.AppointmentDate == proposedAppointment.AppointmentDate &&
                !existing.IsArchived &&
                (existing.Status == "Scheduled" ||
                 existing.Status == "Confirmed") &&
                existing.Id != proposedAppointment.Id)
            .ToListAsync(cancellationToken);

        TimeSpan proposedStart =
            proposedAppointment.StartTime.ToTimeSpan();

        TimeSpan proposedEnd =
            proposedAppointment.EndTime.Value.ToTimeSpan();

        foreach (var existing in existingAppointments)
        {
            TimeSpan existingStart =
                existing.StartTime.ToTimeSpan();

            TimeSpan existingEnd = existing.EndTime?.ToTimeSpan()
                ?? TimeSpan.FromMinutes(
                    Math.Min(
                        existingStart.TotalMinutes +
                        defaultAppointmentDurationMinutes,
                        TimeSpan.FromDays(1).TotalMinutes));

            bool overlaps =
                proposedStart < existingEnd &&
                existingStart < proposedEnd;

            if (overlaps)
            {
                throw new InvalidOperationException(
                    $"This appointment overlaps with another appointment " +
                    $"scheduled at {existing.StartTime:hh:mm tt}.");
            }
        }
    }

    // VALIDATION HELPERS

    private static void ValidateDefaultDuration(int duration)
    {
        if (duration is < 5 or > 240)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Default appointment duration must be between 5 and 240 minutes.");
        }
    }

    private static void ValidateReportDateRange(
        DateOnly? startDate,
        DateOnly? endDate)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }
    }
}
