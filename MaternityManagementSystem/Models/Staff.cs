namespace MaternityManagementSystem.Models;

public class Staff
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    // Store a password hash, never the original password.
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Nurse/Midwife";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
