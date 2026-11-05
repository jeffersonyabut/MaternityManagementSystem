
namespace MaternityManagementSystem.Models;

public static class StaffRoles
{
    public const string Admin = "Admin";

    public const string Staff = "Staff";

    public static bool IsValid(string? role)
    {
        return string.Equals(
                   role,
                   Admin,
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   role,
                   Staff,
                   StringComparison.OrdinalIgnoreCase);
    }
}
