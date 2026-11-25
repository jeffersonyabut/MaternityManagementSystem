
namespace MaternityManagementSystem.Models;

public class Staff
{
    public int Id { get; set; }

    // Original username casing, preserved for display.
    public string Username { get; set; } = string.Empty;

    // Canonical username used for authentication and uniqueness.
    public string NormalizedUsername { get; set; } = string.Empty;

    // Store a password hash, never the original password.
    public string PasswordHash { get; set; } = string.Empty;

    // Changes whenever existing authenticated sessions must be invalidated.
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public string Role { get; set; } = StaffRoles.Staff;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
