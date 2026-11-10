
namespace MaternityManagementSystem.Sessions;

public sealed class UserSession
{
    public int StaffId { get; }
    public string Username { get; }
    public string Role { get; }
    public string SecurityStamp { get; }

    public UserSession(
        int staffId,
        string username,
        string role,
        string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);

        StaffId = staffId;
        Username = username;
        Role = role;
        SecurityStamp = securityStamp;
    }

    public bool IsAdmin =>
        string.Equals(
            Role,
            "Admin",
            StringComparison.OrdinalIgnoreCase);

    public bool IsStaff =>
        string.Equals(
            Role,
            "Staff",
            StringComparison.OrdinalIgnoreCase);
}
