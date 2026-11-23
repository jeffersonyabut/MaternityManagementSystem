namespace MaternityManagementSystem.Models;

public sealed class AuditLogQuery
{
    public string? SearchTerm { get; set; }

    public int? StaffId { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
