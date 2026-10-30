
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public sealed class AuthorizationService
{
    private readonly SessionService _sessionService;

    public AuthorizationService(SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(sessionService);

        _sessionService = sessionService;
    }

    public bool IsAuthenticated =>
        _sessionService.IsAuthenticated;

    public bool IsAdmin =>
        string.Equals(
            _sessionService.CurrentUser?.Role,
            "Admin",
            StringComparison.OrdinalIgnoreCase);

    public bool IsNurseOrMidwife =>
        string.Equals(
            _sessionService.CurrentUser?.Role,
            "Nurse/Midwife",
            StringComparison.OrdinalIgnoreCase);

    public bool CanManageStaff => IsAdmin;

    public bool CanManageSystemSettings => IsAdmin;

    public bool CanViewAuditLogs => IsAdmin;

    public void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "You must be logged in to perform this action.");
        }
    }

    public void EnsureAdmin()
    {
        EnsureAuthenticated();

        if (!IsAdmin)
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to perform this action.");
        }
    }
}
