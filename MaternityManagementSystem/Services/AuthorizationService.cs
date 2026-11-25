
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public sealed class AuthorizationService
{
    private readonly SessionService _sessionService;
    private readonly StaffRepository _staffRepository;

    public AuthorizationService(SessionService sessionService)
        : this(sessionService, new StaffRepository())
    {
    }

    public AuthorizationService(
        SessionService sessionService,
        StaffRepository staffRepository)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(staffRepository);

        _sessionService = sessionService;
        _staffRepository = staffRepository;
    }

    public bool IsAuthenticated =>
        _sessionService.IsAuthenticated;

    public bool IsAdmin =>
        _sessionService.CurrentUser?.IsAdmin == true;

    public bool IsStaff =>
        _sessionService.CurrentUser?.IsStaff == true;

    public bool CanManageStaff => IsAdmin;

    public bool CanManageSystemSettings => IsAdmin;

    public bool CanViewAuditLogs => IsAdmin;

    public async Task EnsureAuthenticatedAsync()
    {
        var currentUser = _sessionService.CurrentUser;

        if (currentUser is null)
        {
            throw new UnauthorizedAccessException(
                "You must be logged in to perform this action.");
        }

        var staff = await _staffRepository.GetByIdAsync(
            currentUser.StaffId);

        bool sessionIsValid =
            staff is not null &&
            staff.IsActive &&
            string.Equals(
                staff.Role,
                currentUser.Role,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                staff.SecurityStamp,
                currentUser.SecurityStamp,
                StringComparison.Ordinal);

        if (!sessionIsValid)
        {
            _sessionService.ExpireSession();

            throw new UnauthorizedAccessException(
                "Your account is no longer authorized. Please sign in again.");
        }
    }

    public async Task EnsureAdminAsync()
    {
        await EnsureAuthenticatedAsync();

        if (!IsAdmin)
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to perform this action.");
        }
    }
}
