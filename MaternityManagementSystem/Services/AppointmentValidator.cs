
using System;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public static class AppointmentValidator
{
    private static readonly string[] AllowedStatuses =
    {
        "Scheduled",
        "Confirmed",
        "Completed",
        "Cancelled",
        "No-show"
    };

    public static void Validate(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (appointment.PatientId <= 0)
        {
            throw new ArgumentException(
                "A valid patient must be selected.");
        }

        if (appointment.PregnancyId is <= 0)
        {
            throw new ArgumentException(
                "A valid pregnancy must be selected.");
        }

        if (appointment.AppointmentDate == default)
        {
            throw new ArgumentException(
                "A valid appointment date is required.");
        }

        if (appointment.StartTime == default)
        {
            throw new ArgumentException(
                "A valid appointment start time is required.");
        }

        if (appointment.EndTime.HasValue &&
            appointment.EndTime.Value <= appointment.StartTime)
        {
            throw new ArgumentException(
                "Appointment end time must be later than its start time. " +
                "Appointments cannot extend past midnight.");
        }

        if (string.IsNullOrWhiteSpace(appointment.Purpose))
        {
            throw new ArgumentException(
                "Appointment purpose is required.");
        }

        if (appointment.Purpose.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Appointment purpose cannot exceed 200 characters.");
        }

        if (!string.IsNullOrWhiteSpace(appointment.Notes) &&
            appointment.Notes.Trim().Length > 2000)
        {
            throw new ArgumentException(
                "Appointment notes cannot exceed 2000 characters.");
        }

        if (string.IsNullOrWhiteSpace(appointment.Status))
        {
            throw new ArgumentException(
                "Appointment status is required.");
        }

        if (!Array.Exists(
                AllowedStatuses,
                status => string.Equals(
                    status,
                    appointment.Status.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Invalid appointment status.");
        }

        if (string.Equals(
                appointment.Status,
                "Cancelled",
                StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(appointment.CancellationReason))
        {
            throw new ArgumentException(
                "A cancellation reason is required.");
        }

        if (!string.IsNullOrWhiteSpace(appointment.CancellationReason) &&
            appointment.CancellationReason.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Cancellation reason cannot exceed 1000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(appointment.ArchiveReason) &&
            appointment.ArchiveReason.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Archive reason cannot exceed 1000 characters.");
        }

        if (appointment.IsArchived &&
            appointment.ArchivedAtUtc == null)
        {
            throw new ArgumentException(
                "An archived appointment must have an archive date.");
        }

        if (!appointment.IsArchived &&
            appointment.ArchivedAtUtc != null)
        {
            throw new ArgumentException(
                "A non-archived appointment cannot have an archive date.");
        }
    }
}
