using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public sealed record AppointmentSmsReminderSchedule(
    DateTime AppointmentAtUtc,
    DateTime Advance3DayAtUtc,
    DateTime Final24HourAtUtc);

public sealed class AppointmentSmsReminderService
{
    public const string ClinicTimeZoneId = "Singapore Standard Time";

    private static readonly SemaphoreSlim ReconciliationGate = new(1, 1);
    private static readonly TimeZoneInfo ClinicTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(ClinicTimeZoneId);

    private readonly AppointmentSmsReminderRepository _reminderRepository;
    private readonly SystemSettingRepository _systemSettingRepository;

    public AppointmentSmsReminderService(
        AppointmentSmsReminderRepository reminderRepository,
        SystemSettingRepository systemSettingRepository)
    {
        ArgumentNullException.ThrowIfNull(reminderRepository);
        ArgumentNullException.ThrowIfNull(systemSettingRepository);

        _reminderRepository = reminderRepository;
        _systemSettingRepository = systemSettingRepository;
    }

    public static AppointmentSmsReminderSchedule CalculateSchedule(
        DateOnly appointmentDate,
        TimeOnly appointmentStartTime)
    {
        DateTime clinicLocalAppointment = DateTime.SpecifyKind(
            appointmentDate.ToDateTime(appointmentStartTime),
            DateTimeKind.Unspecified);

        DateTime appointmentAtUtc = TimeZoneInfo.ConvertTimeToUtc(
            clinicLocalAppointment,
            ClinicTimeZone);

        return new AppointmentSmsReminderSchedule(
            appointmentAtUtc,
            appointmentAtUtc.AddHours(-72),
            appointmentAtUtc.AddHours(-24));
    }

    public static DateTime TreatAsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc),
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    public async Task ReconcileAppointmentAsync(
        int appointmentId,
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (appointmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(appointmentId));
        }

        DateTime now = TreatAsUtc(nowUtc ?? DateTime.UtcNow);
        await ReconciliationGate.WaitAsync(cancellationToken);
        try
        {
            await ReconcileAppointmentCoreAsync(
                appointmentId,
                now,
                cancellationToken);
        }
        finally
        {
            ReconciliationGate.Release();
        }
    }

    public async Task ReconcilePatientAsync(
        int patientId,
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        await ReconcileManyAsync(nowUtc, patientId, cancellationToken);
    }

    public Task ReconcileAllAsync(
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        return ReconcileManyAsync(nowUtc, null, cancellationToken);
    }

    public async Task<List<AppointmentSmsReminder>> GetEligiblePendingDueAsync(
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        DateTime now = TreatAsUtc(nowUtc ?? DateTime.UtcNow);

        // Reconciliation applies catch-up policy after settings are re-enabled.
        await ReconcileAllAsync(now, cancellationToken);

        if (!await AreGlobalReminderSettingsEnabledAsync(cancellationToken))
        {
            return [];
        }

        List<AppointmentSmsReminder> due =
            await _reminderRepository.FindPendingDueAsync(now, cancellationToken);

        return due
            .Where(reminder =>
                HasUsablePhilippineMobileNumber(
                    reminder.Appointment.Patient.ContactNumber) &&
                CalculateSchedule(
                    reminder.Appointment.AppointmentDate,
                    reminder.Appointment.StartTime).AppointmentAtUtc > now)
            .ToList();
    }

    public async Task<bool> AreGlobalReminderSettingsEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        bool smsEnabled = await ReadBooleanSettingAsync(
            SystemSettingKeys.Notifications.SmsEnabled,
            defaultValue: false,
            cancellationToken);

        bool appointmentRemindersEnabled = await ReadBooleanSettingAsync(
            SystemSettingKeys.Notifications.AppointmentReminders,
            defaultValue: true,
            cancellationToken);

        return smsEnabled && appointmentRemindersEnabled;
    }

    private async Task ReconcileManyAsync(
        DateTime? nowUtc,
        int? patientId,
        CancellationToken cancellationToken)
    {
        DateTime now = TreatAsUtc(nowUtc ?? DateTime.UtcNow);
        DateTime clinicNow = TimeZoneInfo.ConvertTimeFromUtc(now, ClinicTimeZone);
        DateOnly clinicToday = DateOnly.FromDateTime(clinicNow);

        List<int> appointmentIds =
            await _reminderRepository.GetAppointmentIdsForReconciliationAsync(
                clinicToday,
                patientId,
                cancellationToken);

        foreach (int appointmentId in appointmentIds)
        {
            await ReconcileAppointmentAsync(
                appointmentId,
                now,
                cancellationToken);
        }
    }

    private async Task ReconcileAppointmentCoreAsync(
        int appointmentId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        Appointment? appointment =
            await _reminderRepository.GetAppointmentForReconciliationAsync(
                appointmentId,
                cancellationToken);

        if (appointment is null)
        {
            return;
        }

        DateTime appointmentAtUtc = CalculateSchedule(
            appointment.AppointmentDate,
            appointment.StartTime).AppointmentAtUtc;

        string? ineligibleReason = GetIneligibleReason(
            appointment,
            appointmentAtUtc,
            nowUtc);

        if (ineligibleReason is not null)
        {
            await _reminderRepository.SkipAllPendingAsync(
                appointment.Id,
                ineligibleReason,
                nowUtc,
                cancellationToken);
            return;
        }

        await _reminderRepository.SupersedePendingForOldRevisionsAsync(
            appointment.Id,
            appointment.ScheduleRevision,
            nowUtc,
            cancellationToken);

        await _reminderRepository.SkipPendingForOtherConsentGenerationsAsync(
            appointment.Id,
            appointment.Patient.SmsReminderConsentGeneration,
            nowUtc,
            cancellationToken);

        List<AppointmentSmsReminder> existing =
            await _reminderRepository.GetByAppointmentAndRevisionAsync(
                appointment.Id,
                appointment.ScheduleRevision,
                cancellationToken);

        int consentGeneration = appointment.Patient.SmsReminderConsentGeneration;
        AppointmentSmsReminderSchedule schedule = CalculateSchedule(
            appointment.AppointmentDate,
            appointment.StartTime);

        var currentGenerationRows = existing
            .Where(reminder => reminder.ConsentGeneration == consentGeneration)
            .ToList();

        DateTime now = TreatAsUtc(nowUtc);
        DateTime createdAtUtc = now;
        var missing = new List<AppointmentSmsReminder>();

        if (!currentGenerationRows.Any(reminder =>
                reminder.ReminderType == AppointmentSmsReminderType.Advance3Day))
        {
            missing.Add(CreatePendingReminder(
                appointment,
                consentGeneration,
                AppointmentSmsReminderType.Advance3Day,
                schedule.Advance3DayAtUtc,
                createdAtUtc));
        }

        if (!currentGenerationRows.Any(reminder =>
                reminder.ReminderType == AppointmentSmsReminderType.Final24Hour))
        {
            missing.Add(CreatePendingReminder(
                appointment,
                consentGeneration,
                AppointmentSmsReminderType.Final24Hour,
                schedule.Final24HourAtUtc,
                createdAtUtc));
        }

        await _reminderRepository.CreatePendingIdempotentlyAsync(
            missing,
            cancellationToken);

        bool globalSettingsEnabled =
            await AreGlobalReminderSettingsEnabledAsync(cancellationToken);

        if (!globalSettingsEnabled ||
            schedule.Advance3DayAtUtc > now ||
            schedule.Final24HourAtUtc > now)
        {
            return;
        }

        // If both are due, only the final reminder remains pending. The
        // repository update is conditional on Pending, preserving all terminal
        // and Sending rows.
        await _reminderRepository.SupersedePendingReminderAsync(
            appointment.Id,
            appointment.ScheduleRevision,
            consentGeneration,
            AppointmentSmsReminderType.Advance3Day,
            now,
            cancellationToken);
    }

    private static string? GetIneligibleReason(
        Appointment appointment,
        DateTime appointmentAtUtc,
        DateTime nowUtc)
    {
        if (appointmentAtUtc <= nowUtc)
        {
            return "appointment_passed";
        }

        if (appointment.IsArchived)
        {
            return "appointment_archived";
        }

        if (appointment.Status is not ("Scheduled" or "Confirmed"))
        {
            return "appointment_status_ineligible";
        }

        if (!appointment.Patient.HasSmsReminderConsent)
        {
            return "consent_withdrawn";
        }

        if (!HasUsablePhilippineMobileNumber(appointment.Patient.ContactNumber))
        {
            return "contact_unavailable";
        }

        return null;
    }

    private static AppointmentSmsReminder CreatePendingReminder(
        Appointment appointment,
        int consentGeneration,
        AppointmentSmsReminderType reminderType,
        DateTime scheduledAtUtc,
        DateTime createdAtUtc)
    {
        return new AppointmentSmsReminder
        {
            AppointmentId = appointment.Id,
            ScheduleRevision = appointment.ScheduleRevision,
            ConsentGeneration = consentGeneration,
            ReminderType = reminderType,
            ScheduledAtUtc = TreatAsUtc(scheduledAtUtc),
            AppointmentDateSnapshot = appointment.AppointmentDate,
            AppointmentStartTimeSnapshot = appointment.StartTime,
            Status = AppointmentSmsReminderStatus.Pending,
            CreatedAtUtc = createdAtUtc,
            StatusChangedAtUtc = createdAtUtc
        };
    }

    private async Task<bool> ReadBooleanSettingAsync(
        string key,
        bool defaultValue,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var setting = await _systemSettingRepository.GetByKeyAsync(key);
        return setting is null
            ? defaultValue
            : bool.TryParse(setting.Value, out bool enabled) && enabled;
    }

    private static bool HasUsablePhilippineMobileNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return normalized.Length == 11 &&
               normalized.StartsWith("09", StringComparison.Ordinal) &&
               normalized[2..].All(char.IsAsciiDigit) ||
               normalized.Length == 13 &&
               normalized.StartsWith("+639", StringComparison.Ordinal) &&
               normalized[4..].All(char.IsAsciiDigit);
    }
}
