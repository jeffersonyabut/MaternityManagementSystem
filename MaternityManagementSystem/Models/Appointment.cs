
using System;

namespace MaternityManagementSystem.Models;

public class Appointment
{
    public int Id { get; set; }

    // Patient relationship
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    // Optional pregnancy relationship
    public int? PregnancyId { get; set; }
    public Pregnancy? Pregnancy { get; set; }

    // Appointment schedule
    public DateOnly AppointmentDate { get; set; }

    public TimeOnly StartTime { get; set; }

    // Incremented only when the clinic-local appointment date or start time changes.
    public int ScheduleRevision { get; set; } = 1;

    public TimeOnly? EndTime { get; set; }

    // Appointment details
    public string Purpose { get; set; } = string.Empty;

    public string? Notes { get; set; }

    // Appointment status
    public string Status { get; set; } = "Scheduled";

    // Cancellation information
    public string? CancellationReason { get; set; }

    // Record timestamps
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    // Archive information
    public bool IsArchived { get; set; } = false;

    public DateTime? ArchivedAtUtc { get; set; }

    public string? ArchiveReason { get; set; }
}
