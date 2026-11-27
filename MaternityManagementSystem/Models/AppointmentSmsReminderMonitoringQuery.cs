namespace MaternityManagementSystem.Models;

/// <summary>Filters and pages the read-only appointment reminder monitoring view.</summary>
public sealed class AppointmentSmsReminderMonitoringQuery
{
    public AppointmentSmsReminderStatus? Status { get; init; }
    public AppointmentSmsReminderType? ReminderType { get; init; }
    public DateOnly? AppointmentDateFrom { get; init; }
    public DateOnly? AppointmentDateTo { get; init; }
    public string? SearchText { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// A deliberately flat, non-clinical projection for reminder monitoring.
/// Lifecycle timestamp values remain UTC; appointment snapshots remain clinic-local.
/// </summary>
public sealed record AppointmentSmsReminderMonitoringRecord(
    int ReminderId,
    string PatientFirstName,
    string PatientLastName,
    string? PatientNumber,
    DateOnly AppointmentDate,
    TimeOnly AppointmentStartTime,
    AppointmentSmsReminderType ReminderType,
    AppointmentSmsReminderStatus Status,
    string? SanitizedOutcomeCode,
    string? ProviderStatus,
    DateTime ScheduledAtUtc,
    DateTime CreatedAtUtc,
    DateTime StatusChangedAtUtc,
    DateTime? AttemptStartedAtUtc,
    DateTime? FinalizedAtUtc);

public sealed record AppointmentSmsReminderStatusCount(
    AppointmentSmsReminderStatus Status,
    int Count);

public sealed record AppointmentSmsReminderMonitoringPage(
    IReadOnlyList<AppointmentSmsReminderMonitoringRecord> Records,
    int TotalCount,
    int PageNumber,
    int PageSize);
