using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public sealed class AppointmentSmsReminderRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public AppointmentSmsReminderRepository()
    {
    }

    public AppointmentSmsReminderRepository(
        IDbContextFactory<ClinicDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Returns a patient-safe, no-tracking projection for administrative monitoring.
    /// Appointment snapshots are clinic-local values; lifecycle timestamps are UTC.
    /// </summary>
    public async Task<AppointmentSmsReminderMonitoringPage> GetMonitoringPageAsync(
        AppointmentSmsReminderMonitoringQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageNumber));
        }

        if (query.PageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        }

        if (query.AppointmentDateFrom is DateOnly from &&
            query.AppointmentDateTo is DateOnly to && from > to)
        {
            throw new ArgumentException(
                "The appointment date range must be in ascending order.", nameof(query));
        }

        int pageSize = Math.Min(query.PageSize, 200);
        int offset = checked((query.PageNumber - 1) * pageSize);

        await using var context = await CreateDbContextAsync(cancellationToken);
        IQueryable<AppointmentSmsReminder> reminders = context.AppointmentSmsReminders
            .AsNoTracking();

        if (query.Status.HasValue)
        {
            reminders = reminders.Where(reminder => reminder.Status == query.Status.Value);
        }

        if (query.ReminderType.HasValue)
        {
            reminders = reminders.Where(
                reminder => reminder.ReminderType == query.ReminderType.Value);
        }

        if (query.AppointmentDateFrom is DateOnly appointmentDateFrom)
        {
            reminders = reminders.Where(
                reminder => reminder.AppointmentDateSnapshot >= appointmentDateFrom);
        }

        if (query.AppointmentDateTo is DateOnly appointmentDateTo)
        {
            reminders = reminders.Where(
                reminder => reminder.AppointmentDateSnapshot <= appointmentDateTo);
        }

        string search = query.SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
        if (search.Length > 0)
        {
            reminders = reminders.Where(reminder =>
                (reminder.Appointment.Patient.FirstName + " " +
                 reminder.Appointment.Patient.LastName).ToLower().Contains(search) ||
                reminder.Appointment.Patient.FirstName.ToLower().Contains(search) ||
                reminder.Appointment.Patient.LastName.ToLower().Contains(search) ||
                reminder.Appointment.Patient.PatientNumber.ToLower().Contains(search));
        }

        int totalCount = await reminders.CountAsync(cancellationToken);

        // Pending and Sending items are current queue work and sort by scheduled
        // time. All other statuses are historical and sort newest status change
        // first. Reminder ID breaks ties deterministically in either group.
        List<AppointmentSmsReminderMonitoringRecord> page = await reminders
            .OrderBy(reminder =>
                reminder.Status == AppointmentSmsReminderStatus.Pending ||
                reminder.Status == AppointmentSmsReminderStatus.Sending ? 0 : 1)
            .ThenBy(reminder =>
                reminder.Status == AppointmentSmsReminderStatus.Pending ||
                reminder.Status == AppointmentSmsReminderStatus.Sending
                    ? reminder.ScheduledAtUtc
                    : DateTime.MaxValue)
            .ThenByDescending(reminder =>
                reminder.Status == AppointmentSmsReminderStatus.Pending ||
                reminder.Status == AppointmentSmsReminderStatus.Sending
                    ? DateTime.MinValue
                    : reminder.StatusChangedAtUtc)
            .ThenByDescending(reminder => reminder.Id)
            .Skip(offset)
            .Take(pageSize)
            .Select(reminder => new AppointmentSmsReminderMonitoringRecord(
                reminder.Id,
                reminder.Appointment.Patient.FirstName,
                reminder.Appointment.Patient.LastName,
                reminder.Appointment.Patient.PatientNumber,
                reminder.AppointmentDateSnapshot,
                reminder.AppointmentStartTimeSnapshot,
                reminder.ReminderType,
                reminder.Status,
                reminder.SanitizedOutcomeCode,
                reminder.ProviderStatus,
                reminder.ScheduledAtUtc,
                reminder.CreatedAtUtc,
                reminder.StatusChangedAtUtc,
                reminder.AttemptStartedAtUtc,
                reminder.FinalizedAtUtc))
            .ToListAsync(cancellationToken);

        // SQLite materializes DateTime values as Unspecified. Restore only the
        // UTC kind metadata in memory; these are already UTC ticks and must not
        // be converted. Appointment snapshots are clinic-local and stay intact.
        for (int index = 0; index < page.Count; index++)
        {
            AppointmentSmsReminderMonitoringRecord record = page[index];
            page[index] = record with
            {
                ScheduledAtUtc = SpecifyUtcKind(record.ScheduledAtUtc),
                CreatedAtUtc = SpecifyUtcKind(record.CreatedAtUtc),
                StatusChangedAtUtc = SpecifyUtcKind(record.StatusChangedAtUtc),
                AttemptStartedAtUtc = SpecifyUtcKind(record.AttemptStartedAtUtc),
                FinalizedAtUtc = SpecifyUtcKind(record.FinalizedAtUtc)
            };
        }

        return new AppointmentSmsReminderMonitoringPage(
            page, totalCount, query.PageNumber, pageSize);
    }

    /// <summary>Returns counts for all reminder statuses, including statuses with no rows.</summary>
    public async Task<IReadOnlyList<AppointmentSmsReminderStatusCount>>
        GetMonitoringStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateDbContextAsync(cancellationToken);
        var groupedCounts = await context.AppointmentSmsReminders
            .AsNoTracking()
            .GroupBy(reminder => reminder.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var countsByStatus = groupedCounts.ToDictionary(item => item.Status, item => item.Count);
        return Enum.GetValues<AppointmentSmsReminderStatus>()
            .Select(status => new AppointmentSmsReminderStatusCount(
                status,
                countsByStatus.GetValueOrDefault(status)))
            .ToArray();
    }

    private async Task<ClinicDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            return await _contextFactory.CreateDbContextAsync(cancellationToken);
        }

        return new ClinicDbContext();
    }

    public async Task<Appointment?> GetAppointmentForReconciliationAsync(
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateDbContextAsync(cancellationToken);
        return await context.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .FirstOrDefaultAsync(
                appointment => appointment.Id == appointmentId,
                cancellationToken);
    }

    public async Task<List<int>> GetAppointmentIdsForReconciliationAsync(
        DateOnly clinicToday,
        int? patientId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateDbContextAsync(cancellationToken);

        var activeAppointmentIds = context.Appointments
            .Where(appointment =>
                !appointment.IsArchived &&
                appointment.AppointmentDate >= clinicToday &&
                (appointment.Status == "Scheduled" ||
                 appointment.Status == "Confirmed"));

        var pendingAppointmentIds = context.AppointmentSmsReminders
            .Where(reminder => reminder.Status == AppointmentSmsReminderStatus.Pending)
            .Select(reminder => reminder.AppointmentId);

        if (patientId.HasValue)
        {
            activeAppointmentIds = activeAppointmentIds.Where(
                appointment => appointment.PatientId == patientId.Value);
            pendingAppointmentIds = pendingAppointmentIds.Where(
                id => context.Appointments.Any(appointment =>
                    appointment.Id == id && appointment.PatientId == patientId.Value));
        }

        return await activeAppointmentIds
            .Select(appointment => appointment.Id)
            .Concat(pendingAppointmentIds)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AppointmentSmsReminder>> GetByAppointmentAndRevisionAsync(
        int appointmentId,
        int scheduleRevision,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateDbContextAsync(cancellationToken);
        List<AppointmentSmsReminder> reminders = await context.AppointmentSmsReminders
            .AsNoTracking()
            .Where(reminder =>
                reminder.AppointmentId == appointmentId &&
                reminder.ScheduleRevision == scheduleRevision)
            .OrderBy(reminder => reminder.ConsentGeneration)
            .ThenBy(reminder => reminder.ReminderType)
            .ToListAsync(cancellationToken);

        NormalizeUtcFields(reminders);
        return reminders;
    }

    public async Task<List<AppointmentSmsReminder>> FindPendingDueAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        nowUtc = TreatAsUtc(nowUtc);

        await using var context = await CreateDbContextAsync(cancellationToken);
        List<AppointmentSmsReminder> reminders = await context.AppointmentSmsReminders
            .AsNoTracking()
            .Include(reminder => reminder.Appointment)
                .ThenInclude(appointment => appointment.Patient)
            .Where(reminder =>
                reminder.Status == AppointmentSmsReminderStatus.Pending &&
                reminder.ScheduledAtUtc <= nowUtc &&
                reminder.ScheduleRevision == reminder.Appointment.ScheduleRevision &&
                reminder.ConsentGeneration ==
                    reminder.Appointment.Patient.SmsReminderConsentGeneration &&
                reminder.Appointment.Patient.HasSmsReminderConsent &&
                !reminder.Appointment.IsArchived &&
                (reminder.Appointment.Status == "Scheduled" ||
                 reminder.Appointment.Status == "Confirmed"))
            .OrderBy(reminder => reminder.ScheduledAtUtc)
            .ThenBy(reminder => reminder.Id)
            .ToListAsync(cancellationToken);

        NormalizeUtcFields(reminders);
        return reminders;
    }

    /// <summary>
    /// Atomically changes one due, currently eligible Pending reminder to
    /// Sending. The conditional UPDATE is a single SQLite statement, so only
    /// one independent context can observe an affected row. Its implicit
    /// statement transaction ends before any provider request begins.
    /// </summary>
    public async Task<bool> TryClaimPendingReminderAsync(
        int reminderId,
        DateTime nowUtc,
        DateOnly clinicToday,
        TimeOnly clinicTime,
        CancellationToken cancellationToken = default)
    {
        if (reminderId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reminderId));
        }

        nowUtc = TreatAsUtc(nowUtc);
        await using var context = await CreateDbContextAsync(cancellationToken);

        int claimed = await context.AppointmentSmsReminders
            .Where(reminder =>
                reminder.Id == reminderId &&
                reminder.Status == AppointmentSmsReminderStatus.Pending &&
                reminder.ScheduledAtUtc <= nowUtc &&
                reminder.ScheduleRevision == reminder.Appointment.ScheduleRevision &&
                reminder.ConsentGeneration ==
                    reminder.Appointment.Patient.SmsReminderConsentGeneration &&
                reminder.Appointment.Patient.HasSmsReminderConsent &&
                !reminder.Appointment.IsArchived &&
                (reminder.Appointment.Status == "Scheduled" ||
                 reminder.Appointment.Status == "Confirmed") &&
                (reminder.Appointment.AppointmentDate > clinicToday ||
                 (reminder.Appointment.AppointmentDate == clinicToday &&
                  reminder.Appointment.StartTime > clinicTime)))
            .ExecuteUpdateAsync(update => update
                .SetProperty(reminder => reminder.Status,
                    AppointmentSmsReminderStatus.Sending)
                .SetProperty(reminder => reminder.AttemptStartedAtUtc, nowUtc)
                .SetProperty(reminder => reminder.StatusChangedAtUtc, nowUtc)
                .SetProperty(reminder => reminder.FinalizedAtUtc, (DateTime?)null)
                .SetProperty(reminder => reminder.SanitizedOutcomeCode, (string?)null),
                cancellationToken);

        return claimed == 1;
    }

    public async Task<AppointmentSmsReminder?> GetClaimedForDispatchAsync(
        int reminderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateDbContextAsync(cancellationToken);
        AppointmentSmsReminder? reminder = await context.AppointmentSmsReminders
            .AsNoTracking()
            .Include(item => item.Appointment)
                .ThenInclude(appointment => appointment.Patient)
            .FirstOrDefaultAsync(item =>
                    item.Id == reminderId &&
                    item.Status == AppointmentSmsReminderStatus.Sending,
                cancellationToken);

        if (reminder is not null)
        {
            NormalizeUtcFields([reminder]);
        }

        return reminder;
    }

    public async Task<bool> FinalizeClaimedReminderAsync(
        int reminderId,
        AppointmentSmsReminderStatus finalStatus,
        DateTime changedAtUtc,
        string sanitizedOutcomeCode,
        string? providerReference = null,
        string? providerStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (finalStatus is not (AppointmentSmsReminderStatus.ProviderAccepted or
            AppointmentSmsReminderStatus.Failed or
            AppointmentSmsReminderStatus.Unknown or
            AppointmentSmsReminderStatus.Superseded or
            AppointmentSmsReminderStatus.Skipped))
        {
            throw new ArgumentOutOfRangeException(nameof(finalStatus));
        }

        ValidateReasonCode(sanitizedOutcomeCode);
        if (providerReference is { Length: > 200 })
        {
            throw new ArgumentException(
                "Provider reference cannot exceed 200 characters.",
                nameof(providerReference));
        }

        if (providerStatus is { Length: > 100 })
        {
            throw new ArgumentException(
                "Provider status cannot exceed 100 characters.",
                nameof(providerStatus));
        }

        changedAtUtc = TreatAsUtc(changedAtUtc);
        await using var context = await CreateDbContextAsync(cancellationToken);

        return await context.AppointmentSmsReminders
            .Where(reminder =>
                reminder.Id == reminderId &&
                reminder.Status == AppointmentSmsReminderStatus.Sending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(reminder => reminder.Status, finalStatus)
                .SetProperty(reminder => reminder.StatusChangedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.FinalizedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.SanitizedOutcomeCode,
                    sanitizedOutcomeCode)
                .SetProperty(reminder => reminder.ProviderReference,
                    providerReference)
                .SetProperty(reminder => reminder.ProviderStatus,
                    providerStatus),
                cancellationToken) == 1;
    }

    public async Task<bool> ReleaseClaimedReminderBeforeRequestAsync(
        int reminderId,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        changedAtUtc = TreatAsUtc(changedAtUtc);
        await using var context = await CreateDbContextAsync(cancellationToken);

        return await context.AppointmentSmsReminders
            .Where(reminder =>
                reminder.Id == reminderId &&
                reminder.Status == AppointmentSmsReminderStatus.Sending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(reminder => reminder.Status,
                    AppointmentSmsReminderStatus.Pending)
                .SetProperty(reminder => reminder.AttemptStartedAtUtc,
                    (DateTime?)null)
                .SetProperty(reminder => reminder.StatusChangedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.FinalizedAtUtc, (DateTime?)null)
                .SetProperty(reminder => reminder.SanitizedOutcomeCode, (string?)null)
                .SetProperty(reminder => reminder.ProviderReference, (string?)null)
                .SetProperty(reminder => reminder.ProviderStatus, (string?)null),
                cancellationToken) == 1;
    }

    public async Task<int> SupersedePendingForOldRevisionsAsync(
        int appointmentId,
        int currentScheduleRevision,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        changedAtUtc = TreatAsUtc(changedAtUtc);

        await using var context = await CreateDbContextAsync(cancellationToken);
        return await context.AppointmentSmsReminders
            .Where(reminder =>
                reminder.AppointmentId == appointmentId &&
                reminder.ScheduleRevision != currentScheduleRevision &&
                reminder.Status == AppointmentSmsReminderStatus.Pending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(reminder => reminder.Status,
                    AppointmentSmsReminderStatus.Superseded)
                .SetProperty(reminder => reminder.StatusChangedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.FinalizedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.SanitizedOutcomeCode,
                    "schedule_revision_superseded"),
                cancellationToken);
    }

    public async Task<int> SkipPendingForOtherConsentGenerationsAsync(
        int appointmentId,
        int currentConsentGeneration,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        changedAtUtc = TreatAsUtc(changedAtUtc);

        await using var context = await CreateDbContextAsync(cancellationToken);
        return await context.AppointmentSmsReminders
            .Where(reminder =>
                reminder.AppointmentId == appointmentId &&
                reminder.ConsentGeneration != currentConsentGeneration &&
                reminder.Status == AppointmentSmsReminderStatus.Pending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(reminder => reminder.Status,
                    AppointmentSmsReminderStatus.Skipped)
                .SetProperty(reminder => reminder.StatusChangedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.FinalizedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.SanitizedOutcomeCode,
                    "consent_generation_changed"),
                cancellationToken);
    }

    public async Task<int> SkipAllPendingAsync(
        int appointmentId,
        string sanitizedReasonCode,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateReasonCode(sanitizedReasonCode);
        changedAtUtc = TreatAsUtc(changedAtUtc);

        await using var context = await CreateDbContextAsync(cancellationToken);
        return await context.AppointmentSmsReminders
            .Where(reminder =>
                reminder.AppointmentId == appointmentId &&
                reminder.Status == AppointmentSmsReminderStatus.Pending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(reminder => reminder.Status,
                    AppointmentSmsReminderStatus.Skipped)
                .SetProperty(reminder => reminder.StatusChangedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.FinalizedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.SanitizedOutcomeCode,
                    sanitizedReasonCode),
                cancellationToken);
    }

    public async Task<int> SupersedePendingReminderAsync(
        int appointmentId,
        int scheduleRevision,
        int consentGeneration,
        AppointmentSmsReminderType reminderType,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        changedAtUtc = TreatAsUtc(changedAtUtc);

        await using var context = await CreateDbContextAsync(cancellationToken);
        return await context.AppointmentSmsReminders
            .Where(reminder =>
                reminder.AppointmentId == appointmentId &&
                reminder.ScheduleRevision == scheduleRevision &&
                reminder.ConsentGeneration == consentGeneration &&
                reminder.ReminderType == reminderType &&
                reminder.Status == AppointmentSmsReminderStatus.Pending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(reminder => reminder.Status,
                    AppointmentSmsReminderStatus.Superseded)
                .SetProperty(reminder => reminder.StatusChangedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.FinalizedAtUtc, changedAtUtc)
                .SetProperty(reminder => reminder.SanitizedOutcomeCode,
                    "later_reminder_due"),
                cancellationToken);
    }

    public async Task CreatePendingIdempotentlyAsync(
        IReadOnlyCollection<AppointmentSmsReminder> reminders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        if (reminders.Count == 0)
        {
            return;
        }

        await using var context = await CreateDbContextAsync(cancellationToken);
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (AppointmentSmsReminder reminder in reminders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reminder.Status = AppointmentSmsReminderStatus.Pending;

            context.AppointmentSmsReminders.Add(reminder);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when
                (IsUniqueConstraintViolation(exception))
            {
                context.Entry(reminder).State = EntityState.Detached;
                bool exists = await context.AppointmentSmsReminders
                    .AnyAsync(existing =>
                        existing.AppointmentId == reminder.AppointmentId &&
                        existing.ScheduleRevision == reminder.ScheduleRevision &&
                        existing.ConsentGeneration == reminder.ConsentGeneration &&
                        existing.ReminderType == reminder.ReminderType,
                        cancellationToken);

                if (!exists)
                {
                    throw;
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        if (exception is SqliteException sqliteException)
        {
            return sqliteException.SqliteExtendedErrorCode is 1555 or 2067;
        }

        return exception.InnerException is not null &&
               IsUniqueConstraintViolation(exception.InnerException);
    }

    private static void ValidateReasonCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 100 ||
            value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException(
                "A sanitized reminder outcome code is required.",
                nameof(value));
        }
    }

    private static DateTime TreatAsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
    }

    private static DateTime SpecifyUtcKind(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? SpecifyUtcKind(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static void NormalizeUtcFields(
        IEnumerable<AppointmentSmsReminder> reminders)
    {
        foreach (AppointmentSmsReminder reminder in reminders)
        {
            reminder.ScheduledAtUtc = TreatAsUtc(reminder.ScheduledAtUtc);
            reminder.CreatedAtUtc = TreatAsUtc(reminder.CreatedAtUtc);
            reminder.StatusChangedAtUtc = TreatAsUtc(reminder.StatusChangedAtUtc);
            reminder.AttemptStartedAtUtc = reminder.AttemptStartedAtUtc is DateTime startedAtUtc
                ? TreatAsUtc(startedAtUtc)
                : null;
            reminder.FinalizedAtUtc = reminder.FinalizedAtUtc is DateTime finalizedAtUtc
                ? TreatAsUtc(finalizedAtUtc)
                : null;
        }
    }
}
