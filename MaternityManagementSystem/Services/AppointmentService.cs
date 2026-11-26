
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public class AppointmentService
{
    private readonly RegionalSettingsService _regionalSettingsService;
    private readonly SystemSettingService _systemSettingService;
    private readonly AppointmentRepository _appointmentRepository;
    private readonly PatientRepository _patientRepository;
    private readonly PregnancyRepository _pregnancyRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly SessionService _sessionService;
    private readonly AppointmentSmsReminderService? _reminderService;

    public AppointmentService(
        AppointmentRepository appointmentRepository,
        PatientRepository patientRepository,
        PregnancyRepository pregnancyRepository,
        AuthorizationService authorizationService,
        SessionService sessionService,
        RegionalSettingsService regionalSettingsService,
        SystemSettingService systemSettingService,
        AppointmentSmsReminderService? reminderService = null)
    {
        ArgumentNullException.ThrowIfNull(appointmentRepository);
        ArgumentNullException.ThrowIfNull(patientRepository);
        ArgumentNullException.ThrowIfNull(pregnancyRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);
        ArgumentNullException.ThrowIfNull(systemSettingService);

        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _pregnancyRepository = pregnancyRepository;
        _authorizationService = authorizationService;
        _sessionService = sessionService;
        _regionalSettingsService = regionalSettingsService;
        _systemSettingService = systemSettingService;
        _reminderService = reminderService;
    }

    // CREATE

    public async Task<Appointment> CreateAppointmentAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(appointment);

        await ValidatePatientAndPregnancyAsync(
            appointment.PatientId,
            appointment.PregnancyId,
            cancellationToken);

        NormalizeFields(appointment);

        if (IsAppointmentInPast(
                appointment.AppointmentDate,
                appointment.StartTime))
        {
            throw new ArgumentException(
                "An appointment cannot be scheduled in the past. " +
                "Please select a future date and time.");
        }

        int defaultDuration =
            await GetDefaultAppointmentDurationMinutesAsync();

        ApplyDefaultDuration(appointment, defaultDuration);

        AppointmentValidator.Validate(appointment);

        await EnsureNoScheduleConflictAsync(
            appointment,
            defaultDuration,
            cancellationToken);

        appointment.CreatedAtUtc = DateTime.UtcNow;
        appointment.UpdatedAtUtc = null;

        appointment.Status = "Scheduled";
        appointment.CancellationReason = null;

        appointment.IsArchived = false;
        appointment.ArchivedAtUtc = null;
        appointment.ArchiveReason = null;

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Created",
            EntityType = "Appointment",
            Details = "Appointment scheduled."
        };

        Appointment createdAppointment = await _appointmentRepository.AddAsync(
            appointment,
            auditLog,
            defaultDuration,
            cancellationToken);

        await TryReconcileReminderAsync(createdAppointment.Id, cancellationToken);

        return createdAppointment;
    }

    // RETRIEVE

    public async Task<Appointment?> GetByIdAsync(
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (appointmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appointmentId));
        }

        return await _appointmentRepository.GetByIdAsync(
            appointmentId,
            cancellationToken);
    }

    public async Task<List<Appointment>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        return await _appointmentRepository.GetAllAsync(
            cancellationToken);
    }

    public async Task<List<Appointment>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        return await _appointmentRepository.GetByPatientIdAsync(
            patientId,
            cancellationToken);
    }

    public async Task<List<Appointment>> GetByPregnancyIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (pregnancyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancyId));
        }

        return await _appointmentRepository.GetByPregnancyIdAsync(
            pregnancyId,
            cancellationToken);
    }

    public async Task<List<Appointment>> GetByDateRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (endDate < startDate)
        {
            throw new ArgumentException(
                "The end date cannot be earlier than the start date.");
        }

        return await _appointmentRepository.GetByDateRangeAsync(
            startDate,
            endDate,
            cancellationToken);
    }

    public async Task<List<Appointment>> GetForReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        string? searchTerm,
        string? status,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            string normalizedStatus = status.Trim();

            string[] allowedStatuses =
            [
                "Scheduled",
                "Confirmed",
                "Completed",
                "Cancelled",
                "No-show"
            ];

            if (!normalizedStatus.Equals(
                    "All",
                    StringComparison.OrdinalIgnoreCase) &&
                !allowedStatuses.Contains(
                    normalizedStatus,
                    StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Invalid appointment status.",
                    nameof(status));
            }
        }

        return await _appointmentRepository.GetForReportAsync(
            startDate,
            endDate,
            searchTerm,
            status,
            cancellationToken);
    }

    // CLINIC ACTIVITY REPORT

    public async Task<int> GetAppointmentCountForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _appointmentRepository
            .GetAppointmentCountForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<List<DateOnly>> GetAppointmentDatesForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _appointmentRepository
            .GetAppointmentDatesForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<List<int>> GetPatientIdsForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _appointmentRepository
            .GetPatientIdsForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<List<string>> GetStatusesForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _appointmentRepository
            .GetStatusesForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    // UPDATE / RESCHEDULE

    public async Task<Appointment> UpdateAppointmentAsync(
        Appointment updatedAppointment,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(updatedAppointment);

        if (updatedAppointment.Id <= 0)
        {
            throw new ArgumentException(
                "A valid appointment ID is required.",
                nameof(updatedAppointment));
        }

        var existingAppointment =
            await _appointmentRepository.GetByIdAsync(
                updatedAppointment.Id,
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

        if (existingAppointment.PatientId != updatedAppointment.PatientId)
        {
            throw new InvalidOperationException(
                "An appointment cannot be reassigned to another patient.");
        }

        if (existingAppointment.PregnancyId != updatedAppointment.PregnancyId)
        {
            throw new InvalidOperationException(
                "An appointment cannot be reassigned to another pregnancy.");
        }

        NormalizeFields(updatedAppointment);

        bool scheduleChanged =
            existingAppointment.AppointmentDate !=
                updatedAppointment.AppointmentDate ||
            existingAppointment.StartTime !=
                updatedAppointment.StartTime;

        if (scheduleChanged &&
            IsAppointmentInPast(
                updatedAppointment.AppointmentDate,
                updatedAppointment.StartTime))
        {
            throw new ArgumentException(
                "An appointment cannot be rescheduled into the past. " +
                "Please select a future date and time.");
        }

        int defaultDuration =
            await GetDefaultAppointmentDurationMinutesAsync();

        ApplyDefaultDuration(updatedAppointment, defaultDuration);

        AppointmentValidator.Validate(updatedAppointment);

        await EnsureNoScheduleConflictAsync(
            updatedAppointment,
            defaultDuration,
            cancellationToken);

        existingAppointment.AppointmentDate =
            updatedAppointment.AppointmentDate;

        existingAppointment.StartTime =
            updatedAppointment.StartTime;

        existingAppointment.EndTime =
            updatedAppointment.EndTime;

        existingAppointment.Purpose =
            updatedAppointment.Purpose;

        existingAppointment.Notes =
            updatedAppointment.Notes;

        existingAppointment.Status =
            updatedAppointment.Status;

        existingAppointment.CancellationReason =
            updatedAppointment.CancellationReason;

        existingAppointment.UpdatedAtUtc = DateTime.UtcNow;

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Updated",
            EntityType = "Appointment",
            EntityId = existingAppointment.Id,
            Details = "Appointment details or schedule updated."
        };

        await _appointmentRepository.UpdateAsync(
            existingAppointment,
            auditLog,
            defaultDuration,
            cancellationToken);

        await TryReconcileReminderAsync(existingAppointment.Id, cancellationToken);

        // Keep the returned instance consistent with the repository's
        // persisted revision, which is derived from the stored schedule.
        if (scheduleChanged)
        {
            existingAppointment.ScheduleRevision =
                checked(existingAppointment.ScheduleRevision + 1);
        }

        return existingAppointment;
    }

    // CANCEL

    public async Task CancelAppointmentAsync(
        int appointmentId,
        string? cancellationReason,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (appointmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appointmentId));
        }

        if (string.IsNullOrWhiteSpace(cancellationReason))
        {
            throw new ArgumentException(
                "A cancellation reason is required.",
                nameof(cancellationReason));
        }

        string normalizedReason = cancellationReason.Trim();

        if (normalizedReason.Length > 1000)
        {
            throw new ArgumentException(
                "Cancellation reason cannot exceed 1000 characters.");
        }

        var appointment = await _appointmentRepository.GetByIdAsync(
            appointmentId,
            cancellationToken);

        if (appointment is null)
        {
            throw new InvalidOperationException(
                "The appointment could not be found.");
        }

        if (appointment.IsArchived)
        {
            throw new InvalidOperationException(
                "Archived appointments cannot be cancelled.");
        }

        if (appointment.Status is "Completed" or "Cancelled" or "No-show")
        {
            throw new InvalidOperationException(
                "This appointment can no longer be cancelled.");
        }

        appointment.Status = "Cancelled";
        appointment.CancellationReason = normalizedReason;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        int defaultDuration =
            await GetDefaultAppointmentDurationMinutesAsync();

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Cancelled",
            EntityType = "Appointment",
            EntityId = appointmentId,
            Details = "Appointment cancelled.",
            Reason = normalizedReason
        };

        await _appointmentRepository.UpdateAsync(
            appointment,
            auditLog,
            defaultDuration,
            cancellationToken);

        await TryReconcileReminderAsync(appointment.Id, cancellationToken);
    }

    // STATUS

    public async Task UpdateAppointmentStatusAsync(
        int appointmentId,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (appointmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appointmentId));
        }

        if (string.IsNullOrWhiteSpace(newStatus))
        {
            throw new ArgumentException(
                "Appointment status is required.",
                nameof(newStatus));
        }

        string normalizedStatus = newStatus.Trim();

        var appointment = await _appointmentRepository.GetByIdAsync(
            appointmentId,
            cancellationToken);

        if (appointment is null)
        {
            throw new InvalidOperationException(
                "The appointment could not be found.");
        }

        if (appointment.IsArchived)
        {
            throw new InvalidOperationException(
                "Archived appointments cannot be updated.");
        }

        if (normalizedStatus.Equals(
                "Cancelled",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Use CancelAppointmentAsync and provide a cancellation reason.");
        }

        var updatedAppointment = new Appointment
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            PregnancyId = appointment.PregnancyId,
            AppointmentDate = appointment.AppointmentDate,
            StartTime = appointment.StartTime,
            EndTime = appointment.EndTime,
            Purpose = appointment.Purpose,
            Notes = appointment.Notes,
            Status = normalizedStatus,
            CancellationReason = appointment.CancellationReason
        };

        AppointmentValidator.Validate(updatedAppointment);

        appointment.Status = normalizedStatus;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        int defaultDuration =
            await GetDefaultAppointmentDurationMinutesAsync();

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Status Updated",
            EntityType = "Appointment",
            EntityId = appointment.Id,
            Details = $"Appointment status changed to {normalizedStatus}."
        };

        await _appointmentRepository.UpdateAsync(
            appointment,
            auditLog,
            defaultDuration,
            cancellationToken);

        await TryReconcileReminderAsync(appointment.Id, cancellationToken);
    }

    // ARCHIVE

    public async Task ArchiveAppointmentAsync(
        int appointmentId,
        string? archiveReason,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (appointmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appointmentId));
        }

        if (string.IsNullOrWhiteSpace(archiveReason))
        {
            throw new ArgumentException(
                "An archive reason is required.",
                nameof(archiveReason));
        }

        string normalizedReason = archiveReason.Trim();

        if (normalizedReason.Length > 1000)
        {
            throw new ArgumentException(
                "Archive reason cannot exceed 1000 characters.");
        }

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Archived",
            EntityType = "Appointment",
            EntityId = appointmentId,
            Details = "Appointment record archived.",
            Reason = normalizedReason
        };

        await _appointmentRepository.ArchiveAsync(
            appointmentId,
            normalizedReason,
            auditLog,
            cancellationToken);

        await TryReconcileReminderAsync(appointmentId, cancellationToken);
    }

    private async Task TryReconcileReminderAsync(
        int appointmentId,
        CancellationToken cancellationToken)
    {
        if (_reminderService is null)
        {
            return;
        }

        try
        {
            await _reminderService.ReconcileAppointmentAsync(
                appointmentId,
                cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            // The appointment save has already committed. Startup reconciliation
            // will recover pending reminder work without blocking clinic actions.
            System.Diagnostics.Debug.WriteLine(
                "Appointment reminder reconciliation failed; pending work will be retried at startup.");
        }
    }

    // VALIDATION

    private async Task ValidatePatientAndPregnancyAsync(
        int patientId,
        int? pregnancyId,
        CancellationToken cancellationToken)
    {
        if (patientId <= 0)
        {
            throw new ArgumentException(
                "A valid patient must be selected.");
        }

        var patient = await _patientRepository.GetByIdAsync(
            patientId,
            cancellationToken);

        if (patient is null)
        {
            throw new ArgumentException(
                "The selected patient could not be found.");
        }

        if (pregnancyId is null)
        {
            return;
        }

        if (pregnancyId <= 0)
        {
            throw new ArgumentException(
                "A valid pregnancy must be selected.");
        }

        var pregnancy = await _pregnancyRepository.GetByIdAsync(
            pregnancyId.Value,
            cancellationToken);

        if (pregnancy is null)
        {
            throw new ArgumentException(
                "The selected pregnancy could not be found.");
        }

        if (pregnancy.PatientId != patientId)
        {
            throw new InvalidOperationException(
                "The selected pregnancy does not belong to the selected patient.");
        }
    }

    // SCHEDULE CONFLICT DETECTION

    private async Task EnsureNoScheduleConflictAsync(
        Appointment appointment,
        int defaultDurationMinutes,
        CancellationToken cancellationToken)
    {
        var sameDayAppointments =
            await _appointmentRepository.GetByDateRangeAsync(
                appointment.AppointmentDate,
                appointment.AppointmentDate,
                cancellationToken);

        TimeOnly proposedEnd = appointment.EndTime!.Value;

        TimeSpan proposedStart =
            appointment.StartTime.ToTimeSpan();

        TimeSpan proposedEndTime =
            proposedEnd.ToTimeSpan();

        foreach (var existing in sameDayAppointments)
        {
            if (existing.Id == appointment.Id || existing.IsArchived)
            {
                continue;
            }

            if (existing.Status is not ("Scheduled" or "Confirmed"))
            {
                continue;
            }

            TimeSpan existingStart =
                existing.StartTime.ToTimeSpan();

            TimeSpan existingEnd =
                existing.EndTime?.ToTimeSpan()
                ?? TimeSpan.FromMinutes(
                    Math.Min(
                        existingStart.TotalMinutes +
                        defaultDurationMinutes,
                        TimeSpan.FromDays(1).TotalMinutes));

            bool overlaps =
                proposedStart < existingEnd &&
                existingStart < proposedEndTime;

            if (overlaps)
            {
                throw new InvalidOperationException(
                    $"This appointment overlaps with another appointment " +
                    $"scheduled at {existing.StartTime:hh:mm tt}.");
            }
        }
    }

    // CLINIC TIME VALIDATION

    private DateTime GetClinicLocalNow()
    {
        return _regionalSettingsService.ConvertUtcToLocal(
            DateTime.UtcNow);
    }

    private bool IsAppointmentInPast(
        DateOnly appointmentDate,
        TimeOnly startTime)
    {
        DateTime appointmentDateTime =
            appointmentDate.ToDateTime(startTime);

        return appointmentDateTime < GetClinicLocalNow();
    }

    // SYSTEM SETTINGS

    public async Task<int> GetDefaultAppointmentDurationMinutesAsync()
    {
        var setting = await _systemSettingService.GetByKeyAsync(
            SystemSettingKeys.Appointments.DefaultDuration);

        if (int.TryParse(
                setting?.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int duration) &&
            duration >= SystemSettingService.MinimumAppointmentDurationMinutes &&
            duration <= SystemSettingService.MaximumAppointmentDurationMinutes)
        {
            return duration;
        }

        return SystemSettingService.DefaultAppointmentDurationMinutes;
    }

    private static void ApplyDefaultDuration(
        Appointment appointment,
        int defaultDurationMinutes)
    {
        if (appointment.EndTime.HasValue)
        {
            return;
        }

        double proposedEndMinutes =
            appointment.StartTime.ToTimeSpan().TotalMinutes +
            defaultDurationMinutes;

        if (proposedEndMinutes >= TimeSpan.FromDays(1).TotalMinutes)
        {
            throw new ArgumentException(
                "The appointment duration would extend past midnight. " +
                "Please select an earlier start time.");
        }

        appointment.EndTime = appointment.StartTime.AddMinutes(
            defaultDurationMinutes);
    }

    private static void NormalizeFields(Appointment appointment)
    {
        appointment.Purpose =
            appointment.Purpose?.Trim() ?? string.Empty;

        appointment.Notes =
            NormalizeOptionalText(appointment.Notes);

        appointment.Status =
            string.IsNullOrWhiteSpace(appointment.Status)
                ? "Scheduled"
                : appointment.Status.Trim();

        appointment.CancellationReason =
            NormalizeOptionalText(appointment.CancellationReason);

        appointment.ArchiveReason =
            NormalizeOptionalText(appointment.ArchiveReason);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
