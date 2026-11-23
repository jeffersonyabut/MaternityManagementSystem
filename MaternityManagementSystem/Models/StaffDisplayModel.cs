
namespace MaternityManagementSystem.Models;

public class StaffDisplayModel
{
    private static readonly TimeZoneInfo PhilippineTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = StaffRoles.Staff;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Display-only conversion. The database timestamp remains UTC.
    public DateTime CreatedAtPhilippineTime =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc),
            PhilippineTimeZone);
}
