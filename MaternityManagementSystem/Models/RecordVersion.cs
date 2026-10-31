
namespace MaternityManagementSystem.Models;

public class RecordVersion
{
    public int Id { get; set; }

    // Identifies the record being versioned.
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }

    // Stores only the fields that actually changed.
    public string PreviousValuesJson { get; set; } = string.Empty;
    public string NewValuesJson { get; set; } = string.Empty;

    // Identifies who made the changes.
    public int? StaffId { get; set; }
    public string Username { get; set; } = string.Empty;

    // When the changes occurred.
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
