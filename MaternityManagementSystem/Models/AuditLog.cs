namespace MaternityManagementSystem.Models;

public class AuditLog
{
    public int Id { get; set; }

    public int? StaffId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public string? Details { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
