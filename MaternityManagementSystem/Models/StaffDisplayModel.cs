
namespace MaternityManagementSystem.Models;

public class StaffDisplayModel
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = StaffRoles.Staff;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
