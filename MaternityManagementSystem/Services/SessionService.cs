
using System;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public sealed class SessionService
{
    public UserSession? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public event Action? SessionExpired;

    public void StartSession(UserSession user)
    {
        ArgumentNullException.ThrowIfNull(user);

        CurrentUser = user;
    }

    public void EndSession()
    {
        CurrentUser = null;
    }

    public void ExpireSession()
    {
        if (CurrentUser is null)
        {
            return;
        }

        CurrentUser = null;

        SessionExpired?.Invoke();
    }
}
