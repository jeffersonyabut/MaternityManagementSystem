using System;

namespace MaternityManagementSystem.Models;

public enum AppointmentSmsReminderType
{
    Advance3Day,
    Final24Hour
}

public enum AppointmentSmsReminderStatus
{
    Pending,
    Sending,
    // ProviderAccepted means the provider accepted the request; it does not
    // confirm that the recipient received the SMS.
    ProviderAccepted,
    Failed,
    Unknown,
    Superseded,
    Skipped
}

public class AppointmentSmsReminder
{
    public int Id { get; set; }

    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public int ScheduleRevision { get; set; }
    public AppointmentSmsReminderType ReminderType { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateOnly AppointmentDateSnapshot { get; set; }
    public TimeOnly AppointmentStartTimeSnapshot { get; set; }
    public AppointmentSmsReminderStatus Status { get; set; } =
        AppointmentSmsReminderStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime StatusChangedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AttemptStartedAtUtc { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public string? ProviderReference { get; set; }
    public string? ProviderStatus { get; set; }
    public string? SanitizedOutcomeCode { get; set; }
}
