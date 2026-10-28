namespace MaternityManagementSystem.Sessions;

public sealed class UserSession
{
    public int StaffId { get; }
    public string Username { get; }
    public string Role { get; }

    public UserSession(int staffId, string username, string role)
    {
        StaffId = staffId;
        Username = username;
        Role = role;
    }

    public bool IsAdmin =>
        string.Equals(
            Role,
            "Admin",
            StringComparison.OrdinalIgnoreCase);
}
