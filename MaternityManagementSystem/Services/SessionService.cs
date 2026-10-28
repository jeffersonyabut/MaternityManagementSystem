using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public sealed class SessionService
{
    public UserSession? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public void StartSession(UserSession user)
    {
        ArgumentNullException.ThrowIfNull(user);

        CurrentUser = user;
    }

    public void EndSession()
    {
        CurrentUser = null;
    }
}
