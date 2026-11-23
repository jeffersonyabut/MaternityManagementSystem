namespace MaternityManagementSystem.Models;

public sealed class AuditLogSummary
{
    public int TotalActivities { get; set; }

    public int TodayActivities { get; set; }

    public AuditLog? LatestActivity { get; set; }
}
