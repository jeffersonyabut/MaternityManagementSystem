namespace MaternityManagementSystem.Models;

public sealed class AuditLogPage
{
    public IReadOnlyList<AuditLog> Items { get; set; } =
        Array.Empty<AuditLog>();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages =>
        PageSize <= 0
            ? 0
            : (int)Math.Ceiling((double)TotalCount / PageSize);
}
