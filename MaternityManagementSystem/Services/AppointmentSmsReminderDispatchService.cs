using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

/// <summary>
/// Dispatches already-scheduled due reminders. This is a one-pass operation;
/// application polling and lifecycle wiring are intentionally outside this service.
/// </summary>
public sealed class AppointmentSmsReminderDispatchService
{
    private static readonly TimeZoneInfo ClinicTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(
            AppointmentSmsReminderService.ClinicTimeZoneId);

    private readonly AppointmentSmsReminderService _schedulingService;
    private readonly AppointmentSmsReminderRepository _reminderRepository;
    private readonly SystemSettingRepository _systemSettingRepository;
    private readonly IUniSmsClient _uniSmsClient;
    private readonly AppointmentSmsReminderMessageBuilder _messageBuilder;

    public AppointmentSmsReminderDispatchService(
        AppointmentSmsReminderService schedulingService,
        AppointmentSmsReminderRepository reminderRepository,
        SystemSettingRepository systemSettingRepository,
        IUniSmsClient uniSmsClient,
        AppointmentSmsReminderMessageBuilder? messageBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(schedulingService);
        ArgumentNullException.ThrowIfNull(reminderRepository);
        ArgumentNullException.ThrowIfNull(systemSettingRepository);
        ArgumentNullException.ThrowIfNull(uniSmsClient);

        _schedulingService = schedulingService;
        _reminderRepository = reminderRepository;
        _systemSettingRepository = systemSettingRepository;
        _uniSmsClient = uniSmsClient;
        _messageBuilder = messageBuilder ?? new AppointmentSmsReminderMessageBuilder();
    }

    /// <summary>
    /// Reconciles and processes due candidates once. Each provider request is
    /// made only after a conditional claim and a fresh eligibility check.
    /// </summary>
    /// <returns>The number of reminders claimed during this pass.</returns>
    public async Task<int> DispatchDueRemindersAsync(
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        DateTime now = AppointmentSmsReminderService.TreatAsUtc(
            nowUtc ?? DateTime.UtcNow);
        List<AppointmentSmsReminder> candidates =
            await _schedulingService.GetEligiblePendingDueAsync(
                now,
                cancellationToken);

        int claimedCount = 0;
        foreach (AppointmentSmsReminder candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DateTime attemptNowUtc = DateTime.UtcNow;
            if (nowUtc.HasValue)
            {
                attemptNowUtc = now;
            }

            DateTime clinicNow = TimeZoneInfo.ConvertTimeFromUtc(
                attemptNowUtc,
                ClinicTimeZone);

            bool claimed = await _reminderRepository.TryClaimPendingReminderAsync(
                candidate.Id,
                attemptNowUtc,
                DateOnly.FromDateTime(clinicNow),
                TimeOnly.FromDateTime(clinicNow),
                cancellationToken);

            if (!claimed)
            {
                continue;
            }

            claimedCount++;
            try
            {
                await DispatchClaimedReminderAsync(
                    candidate.Id,
                    nowUtc,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // A failed reminder does not prevent other due reminders
                // from being considered during this pass.
            }
        }

        return claimedCount;
    }

    private async Task DispatchClaimedReminderAsync(
        int reminderId,
        DateTime? validationTimeOverrideUtc,
        CancellationToken cancellationToken)
    {
        bool requestMayHaveStarted = false;
        try
        {
            await DispatchClaimedReminderCoreAsync(
                reminderId,
                validationTimeOverrideUtc,
                () => requestMayHaveStarted = true,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (requestMayHaveStarted)
            {
                await PersistOutcomeAsync(
                    reminderId,
                    new UniSmsApiResult(
                        UniSmsOutcome.Unknown,
                        FailureCode: "request_interrupted"),
                    CancellationToken.None);
            }
            else
            {
                await _reminderRepository.ReleaseClaimedReminderBeforeRequestAsync(
                    reminderId,
                    DateTime.UtcNow,
                    CancellationToken.None);
            }

            throw;
        }
        catch (Exception)
        {
            if (requestMayHaveStarted)
            {
                await PersistOutcomeAsync(
                    reminderId,
                    new UniSmsApiResult(
                        UniSmsOutcome.Unknown,
                        FailureCode: "dispatch_error"),
                    CancellationToken.None);
            }

            throw;
        }
    }

    private async Task DispatchClaimedReminderCoreAsync(
        int reminderId,
        DateTime? validationTimeOverrideUtc,
        Action requestStarting,
        CancellationToken cancellationToken)
    {
        AppointmentSmsReminder? reminder =
            await _reminderRepository.GetClaimedForDispatchAsync(
                reminderId,
                cancellationToken);

        if (reminder is null)
        {
            return;
        }

        // Read all global settings together. Then reload the appointment and
        // patient once more so their final check happens immediately before
        // message construction and the provider request.
        List<SystemSetting> settings = await _systemSettingRepository.GetAllAsync();
        bool settingsEnabled =
            ReadBooleanSetting(settings,
                SystemSettingKeys.Notifications.SmsEnabled,
                defaultValue: false) &&
            ReadBooleanSetting(settings,
                SystemSettingKeys.Notifications.AppointmentReminders,
                defaultValue: true);
        if (!settingsEnabled)
        {
            await FinalizeSuppressedAsync(
                reminder.Id,
                AppointmentSmsReminderStatus.Skipped,
                "sms_settings_disabled",
                GetValidationTime(validationTimeOverrideUtc),
                cancellationToken);
            return;
        }

        reminder = await _reminderRepository.GetClaimedForDispatchAsync(
            reminderId,
            cancellationToken);
        if (reminder is null)
        {
            return;
        }

        DateTime validationNowUtc = GetValidationTime(validationTimeOverrideUtc);
        string? ineligibleOutcome = GetIneligibleOutcome(reminder, validationNowUtc);
        if (ineligibleOutcome is not null)
        {
            AppointmentSmsReminderStatus status =
                ineligibleOutcome == "schedule_revision_superseded"
                    ? AppointmentSmsReminderStatus.Superseded
                    : AppointmentSmsReminderStatus.Skipped;
            await FinalizeSuppressedAsync(
                reminder.Id,
                status,
                ineligibleOutcome,
                validationNowUtc,
                cancellationToken);
            return;
        }

        SystemSetting? clinicNameSetting = settings.FirstOrDefault(
            setting => string.Equals(
                setting.Key,
                SystemSettingKeys.Clinic.Name,
                StringComparison.Ordinal));
        string clinicName = string.IsNullOrWhiteSpace(clinicNameSetting?.Value)
            ? SystemSettingDefaults.Values[SystemSettingKeys.Clinic.Name]
            : clinicNameSetting.Value;
        string content = _messageBuilder.Build(
            clinicName,
            reminder.Appointment.AppointmentDate,
            reminder.Appointment.StartTime);

        // No database context or transaction is open across this call. The
        // callback marks the conservative request-start boundary immediately
        // before HttpClient.SendAsync is invoked by UniSmsClient.
        UniSmsApiResult result = await _uniSmsClient.SendAppointmentReminderAsync(
            reminder.Appointment.Patient.ContactNumber!,
            content,
            cancellationToken,
            requestStarting);

        await PersistOutcomeAsync(
            reminder.Id,
            result,
            CancellationToken.None);
    }

    private static string? GetIneligibleOutcome(
        AppointmentSmsReminder reminder,
        DateTime nowUtc)
    {
        Appointment appointment = reminder.Appointment;
        if (reminder.ScheduleRevision != appointment.ScheduleRevision ||
            reminder.AppointmentDateSnapshot != appointment.AppointmentDate ||
            reminder.AppointmentStartTimeSnapshot != appointment.StartTime)
        {
            return "schedule_revision_superseded";
        }

        AppointmentSmsReminderSchedule schedule =
            AppointmentSmsReminderService.CalculateSchedule(
                appointment.AppointmentDate,
                appointment.StartTime);
        if (schedule.AppointmentAtUtc <= nowUtc)
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

        Patient patient = appointment.Patient;
        if (!patient.HasSmsReminderConsent)
        {
            return "consent_withdrawn";
        }

        if (reminder.ConsentGeneration != patient.SmsReminderConsentGeneration)
        {
            return "consent_generation_changed";
        }

        if (!IsValidPhilippineMobileNumber(patient.ContactNumber))
        {
            return "contact_unavailable";
        }

        return null;
    }

    private Task FinalizeSuppressedAsync(
        int reminderId,
        AppointmentSmsReminderStatus status,
        string outcomeCode,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        _reminderRepository.FinalizeClaimedReminderAsync(
            reminderId,
            status,
            nowUtc,
            outcomeCode,
            cancellationToken: cancellationToken);

    private async Task PersistOutcomeAsync(
        int reminderId,
        UniSmsApiResult result,
        CancellationToken cancellationToken)
    {
        (AppointmentSmsReminderStatus status, string outcomeCode) = result.Outcome switch
        {
            UniSmsOutcome.Accepted =>
                (AppointmentSmsReminderStatus.ProviderAccepted, "provider_accepted"),
            UniSmsOutcome.Rejected =>
                (AppointmentSmsReminderStatus.Failed,
                    SanitizeOutcomeCode(result.FailureCode, "provider_rejected")),
            UniSmsOutcome.Unknown =>
                (AppointmentSmsReminderStatus.Unknown,
                    SanitizeOutcomeCode(result.FailureCode, "outcome_unknown")),
            UniSmsOutcome.CredentialUnavailable =>
                (AppointmentSmsReminderStatus.Failed, "credential_unavailable"),
            _ =>
                (AppointmentSmsReminderStatus.Failed, "invalid_provider_request")
        };

        await _reminderRepository.FinalizeClaimedReminderAsync(
            reminderId,
            status,
            DateTime.UtcNow,
            outcomeCode,
            result.ProviderReferenceId,
            result.ProviderStatus,
            cancellationToken);
    }

    private static string SanitizeOutcomeCode(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100 ||
            value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            return fallback;
        }

        return value;
    }

    private static DateTime GetValidationTime(DateTime? validationTimeOverrideUtc) =>
        AppointmentSmsReminderService.TreatAsUtc(
            validationTimeOverrideUtc ?? DateTime.UtcNow);

    private static bool ReadBooleanSetting(
        IEnumerable<SystemSetting> settings,
        string key,
        bool defaultValue)
    {
        SystemSetting? setting = settings.FirstOrDefault(
            item => string.Equals(item.Key, key, StringComparison.Ordinal));
        return setting is null
            ? defaultValue
            : bool.TryParse(setting.Value, out bool enabled) && enabled;
    }

    private static bool IsValidPhilippineMobileNumber(string? value)
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
