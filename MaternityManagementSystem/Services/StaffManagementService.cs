
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public sealed class StaffManagementService
{
    private readonly StaffRepository _staffRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditLogService _auditLogService;
    private readonly SessionService _sessionService;

    public StaffManagementService(
        StaffRepository staffRepository,
        AuthorizationService authorizationService,
        AuditLogService auditLogService,
        SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(staffRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(auditLogService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _staffRepository = staffRepository;
        _authorizationService = authorizationService;
        _auditLogService = auditLogService;
        _sessionService = sessionService;
    }

    public async Task<IReadOnlyList<StaffDisplayModel>> GetAllStaffAsync()
    {
        await _authorizationService.EnsureAdminAsync();

        List<Staff> staffMembers =
            await _staffRepository.GetAllAsync();

        return staffMembers
            .Select(staff => new StaffDisplayModel
            {
                Id = staff.Id,
                Username = staff.Username,
                Role = staff.Role,
                IsActive = staff.IsActive,
                CreatedAtUtc = staff.CreatedAtUtc
            })
            .ToList();
    }

    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        await _authorizationService.EnsureAdminAsync();

        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        string normalizedUsername =
            UsernameNormalizer.Normalize(username);

        return !await _staffRepository.UsernameExistsAsync(
            normalizedUsername);
    }

    public async Task CreateStaffAsync(
        string username,
        string password,
        string role)
    {
        await _authorizationService.EnsureAdminAsync();

        username = username?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "Username is required.");
        }

        if (username.Length > 100)
        {
            throw new ArgumentException(
                "Username cannot exceed 100 characters.");
        }

        string normalizedUsername =
            UsernameNormalizer.Normalize(username);

        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < 12)
        {
            throw new ArgumentException(
                "Password must contain at least 12 characters.");
        }

        if (password.Length > 1024)
        {
            throw new ArgumentException(
                "Password is too long.");
        }

        if (!StaffRoles.IsValid(role))
        {
            throw new ArgumentException(
                "Please select a valid staff role.");
        }

        string normalizedRole =
            string.Equals(
                role,
                StaffRoles.Admin,
                StringComparison.OrdinalIgnoreCase)
                ? StaffRoles.Admin
                : StaffRoles.Staff;

        if (await _staffRepository.UsernameExistsAsync(
            normalizedUsername))
        {
            throw new InvalidOperationException(
                "This username is already taken.");
        }

        Staff staff = new()
        {
            Username = username,
            NormalizedUsername = normalizedUsername,
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = normalizedRole,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        UserSession currentUser = _sessionService.CurrentUser!;

        AuditLog auditLog = new()
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Created",
            EntityType = "Staff",
            Details = $"Created staff account '{username}' with role '{normalizedRole}'.",
            OccurredAtUtc = DateTime.UtcNow
        };

        await _staffRepository.AddWithAuditAsync(
            staff,
            auditLog);
    }

    public async Task UpdateStaffAsync(
        int staffId,
        string username,
        string role)
    {
        await _authorizationService.EnsureAdminAsync();

        username = username?.Trim() ?? string.Empty;

        if (staffId <= 0)
        {
            throw new ArgumentException(
                "Invalid staff account.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "Username is required.");
        }

        if (username.Length > 100)
        {
            throw new ArgumentException(
                "Username cannot exceed 100 characters.");
        }

        string normalizedUsername =
            UsernameNormalizer.Normalize(username);

        if (!StaffRoles.IsValid(role))
        {
            throw new ArgumentException(
                "Please select a valid staff role.");
        }

        string normalizedRole =
            string.Equals(
                role,
                StaffRoles.Admin,
                StringComparison.OrdinalIgnoreCase)
                ? StaffRoles.Admin
                : StaffRoles.Staff;

        Staff? existingStaff =
            await _staffRepository.GetByIdAsync(staffId);

        if (existingStaff is null)
        {
            throw new InvalidOperationException(
                "Staff account was not found.");
        }

        UserSession currentUser = _sessionService.CurrentUser!;

        if (existingStaff.Id == currentUser.StaffId &&
            normalizedRole != StaffRoles.Admin)
        {
            throw new InvalidOperationException(
                "You cannot remove your own administrator role.");
        }

        if (await _staffRepository.UsernameExistsAsync(
            normalizedUsername,
            staffId))
        {
            throw new InvalidOperationException(
                "This username is already taken.");
        }

        string existingNormalizedUsername =
            UsernameNormalizer.Normalize(existingStaff.Username);

        if (existingNormalizedUsername == normalizedUsername &&
            existingStaff.Role == normalizedRole)
        {
            throw new InvalidOperationException(
                "No changes were made.");
        }

        AuditLog auditLog = new()
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Updated",
            EntityType = "Staff",
            Details = $"Updated staff account ID {staffId}.",
            OccurredAtUtc = DateTime.UtcNow
        };

        bool updated = await _staffRepository.UpdateWithAuditAsync(
            staffId,
            username,
            normalizedRole,
            auditLog);

        if (!updated)
        {
            throw new InvalidOperationException(
                "Staff account could not be updated.");
        }
    }

    public async Task SetStaffStatusAsync(
        int staffId,
        bool isActive)
    {
        await _authorizationService.EnsureAdminAsync();

        if (staffId <= 0)
        {
            throw new ArgumentException(
                "Invalid staff account.");
        }

        Staff? staff =
            await _staffRepository.GetByIdAsync(staffId);

        if (staff is null)
        {
            throw new InvalidOperationException(
                "Staff account was not found.");
        }

        if (staff.IsActive == isActive)
        {
            throw new InvalidOperationException(
                isActive
                    ? "This account is already active."
                    : "This account is already inactive.");
        }

        UserSession currentUser = _sessionService.CurrentUser!;

        if (staff.Id == currentUser.StaffId && !isActive)
        {
            throw new InvalidOperationException(
                "You cannot deactivate your own account.");
        }

        if (!isActive && staff.Role == StaffRoles.Admin)
        {
            int activeAdminCount = (await _staffRepository.GetAllAsync())
                .Count(member =>
                    member.IsActive &&
                    member.Role == StaffRoles.Admin);

            if (activeAdminCount <= 1)
            {
                throw new InvalidOperationException(
                    "You cannot deactivate the last active administrator.");
            }
        }

        AuditLog auditLog = new()
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = isActive ? "Activated" : "Deactivated",
            EntityType = "Staff",
            Details = $"Staff account ID {staffId} was {(isActive ? "activated" : "deactivated")}.",
            OccurredAtUtc = DateTime.UtcNow
        };

        bool updated = await _staffRepository.UpdateStatusWithAuditAsync(
            staffId,
            isActive,
            auditLog);

        if (!updated)
        {
            throw new InvalidOperationException(
                "Staff account status could not be updated.");
        }
    }

    public async Task ResetStaffPasswordAsync(
        int staffId,
        string newPassword)
    {
        await _authorizationService.EnsureAdminAsync();

        if (staffId <= 0)
        {
            throw new ArgumentException(
                "Invalid staff account.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) ||
            newPassword.Length < 12)
        {
            throw new ArgumentException(
                "Password must contain at least 12 characters.");
        }

        if (newPassword.Length > 1024)
        {
            throw new ArgumentException(
                "Password is too long.");
        }

        Staff? staff =
            await _staffRepository.GetByIdAsync(staffId);

        if (staff is null)
        {
            throw new InvalidOperationException(
                "Staff account was not found.");
        }

        UserSession currentUser = _sessionService.CurrentUser!;

        if (staff.Id == currentUser.StaffId)
        {
            throw new InvalidOperationException(
                "You cannot reset your own password through Staff Management.");
        }

        string passwordHash =
            PasswordHasher.HashPassword(newPassword);

        AuditLog auditLog = new()
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Password Reset",
            EntityType = "Staff",
            Details = $"Password reset for staff account ID {staffId}.",
            OccurredAtUtc = DateTime.UtcNow
        };

        bool updated = await _staffRepository.ResetPasswordWithAuditAsync(
            staffId,
            passwordHash,
            auditLog);

        if (!updated)
        {
            throw new InvalidOperationException(
                "Staff password could not be reset.");
        }
    }
}
