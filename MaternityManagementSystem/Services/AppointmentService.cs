
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public class AppointmentService
{
    private const int DefaultAppointmentDurationMinutes = 30;

    private static readonly TimeZoneInfo ClinicTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

    private readonly AppointmentRepository _appointmentRepository;
    private readonly PatientRepository _patientRepository;
    private readonly PregnancyRepository _pregnancyRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly SessionService _sessionService;

    public AppointmentService(
        AppointmentRepository appointmentRepository,
        PatientRepository patientRepository,
        PregnancyRepository pregnancyRepository,
        AuthorizationService authorizationService,
        SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(appointmentRepository);
        ArgumentNullException.ThrowIfNull(patientRepository);
        ArgumentNullException.ThrowIfNull(pregnancyRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _pregnancyRepository = pregnancyRepository;
        _authorizationService = authorizationService;
        _sessionService = sessionService;
    }

    // CREATE

    public async Task<Appointment> CreateAppointmentAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

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

        ApplyDefaultDuration(appointment);

        AppointmentValidator.Validate(appointment);

        await EnsureNoScheduleConflictAsync(
            appointment,
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

        return await _appointmentRepository.AddAsync(
            appointment,
            auditLog,
            cancellationToken);
    }

    // RETRIEVE

    public Task<Appointment?> GetByIdAsync(
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        if (appointmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(appointmentId));
        }

        return _appointmentRepository.GetByIdAsync(
            appointmentId,
            cancellationToken);
    }

    public Task<List<Appointment>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        return _appointmentRepository.GetAllAsync(
            cancellationToken);
    }

    public Task<List<Appointment>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        return _appointmentRepository.GetByPatientIdAsync(
            patientId,
            cancellationToken);
    }

    public Task<List<Appointment>> GetByPregnancyIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        if (pregnancyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancyId));
        }

        return _appointmentRepository.GetByPregnancyIdAsync(
            pregnancyId,
            cancellationToken);
    }

    public Task<List<Appointment>> GetByDateRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        if (endDate < startDate)
        {
            throw new ArgumentException(
                "The end date cannot be earlier than the start date.");
        }

        return _appointmentRepository.GetByDateRangeAsync(
            startDate,
            endDate,
            cancellationToken);
    }

    public Task<List<Appointment>> GetForReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    string? searchTerm,
    string? status,
    CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

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

        return _appointmentRepository.GetForReportAsync(
            startDate,
            endDate,
            searchTerm,
            status,
            cancellationToken);
    }



    public async Task<int> GetAppointmentCountForClinicActivityReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

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
        _authorizationService.EnsureAuthenticated();

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
        _authorizationService.EnsureAuthenticated();

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
        _authorizationService.EnsureAuthenticated();

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
        _authorizationService.EnsureAuthenticated();

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

        // Allow corrections to appointment details without changing
        // the schedule of an appointment that is already in the past.
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

        ApplyDefaultDuration(updatedAppointment);

        AppointmentValidator.Validate(updatedAppointment);

        await EnsureNoScheduleConflictAsync(
            updatedAppointment,
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
            cancellationToken);

        return existingAppointment;
    }

    // CANCEL

    public async Task CancelAppointmentAsync(
        int appointmentId,
        string? cancellationReason,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

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
            cancellationToken);
    }

    // STATUS

    public async Task UpdateAppointmentStatusAsync(
        int appointmentId,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

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
            cancellationToken);
    }

    // ARCHIVE

    public async Task ArchiveAppointmentAsync(
        int appointmentId,
        string? archiveReason,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

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

    private async Task EnsureNoScheduleConflictAsync(
        Appointment appointment,
        CancellationToken cancellationToken)
    {
        var sameDayAppointments =
            await _appointmentRepository.GetByDateRangeAsync(
                appointment.AppointmentDate,
                appointment.AppointmentDate,
                cancellationToken);

        TimeOnly proposedEnd = appointment.EndTime!.Value;

        foreach (var existing in sameDayAppointments)
        {
            if (existing.Id == appointment.Id || existing.IsArchived)
            {
                continue;
            }

            // Only scheduled or confirmed appointments reserve a time slot.
            if (existing.Status is not ("Scheduled" or "Confirmed"))
            {
                continue;
            }

            TimeOnly existingEnd = existing.EndTime ??
                existing.StartTime.AddMinutes(
                    DefaultAppointmentDurationMinutes);

            bool overlaps =
                appointment.StartTime < existingEnd &&
                existing.StartTime < proposedEnd;

            if (overlaps)
            {
                throw new InvalidOperationException(
                    $"This appointment overlaps with another appointment " +
                    $"scheduled at {existing.StartTime:hh:mm tt}.");
            }
        }
    }

    // CLINIC TIME VALIDATION

    private static DateTime GetClinicLocalNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            ClinicTimeZone);
    }

    private static bool IsAppointmentInPast(
        DateOnly appointmentDate,
        TimeOnly startTime)
    {
        DateTime appointmentDateTime =
            appointmentDate.ToDateTime(startTime);

        return appointmentDateTime < GetClinicLocalNow();
    }

    private static void ApplyDefaultDuration(Appointment appointment)
    {
        if (appointment.EndTime is null)
        {
            appointment.EndTime = appointment.StartTime.AddMinutes(
                DefaultAppointmentDurationMinutes);
        }
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
