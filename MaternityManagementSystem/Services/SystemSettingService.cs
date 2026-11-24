
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public sealed class SystemSettingService
{
    private readonly SystemSettingRepository _systemSettingRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly SessionService _sessionService;

    public SystemSettingService(
        SystemSettingRepository systemSettingRepository,
        AuthorizationService authorizationService,
        SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(systemSettingRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _systemSettingRepository = systemSettingRepository;
        _authorizationService = authorizationService;
        _sessionService = sessionService;
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ValidateKey(key);

        return await _systemSettingRepository.GetByKeyAsync(key);
    }

    public async Task<List<SystemSetting>> GetAllAsync()
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        return await _systemSettingRepository.GetAllAsync();
    }


    public async Task SaveAsync(string key, string value)
    {
        await _authorizationService.EnsureAdminAsync();

        ValidateKey(key);
        ValidateValue(value);

        var currentUser = _sessionService.CurrentUser
            ?? throw new UnauthorizedAccessException(
                "You must be logged in to perform this action.");

        var existingSetting =
            await _systemSettingRepository.GetByKeyAsync(key);

        var setting = new SystemSetting
        {
            Key = key,
            Value = value,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByStaffId = currentUser.StaffId
        };

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = existingSetting is null
                ? "Create"
                : "Update",
            EntityType = "SystemSetting",
            EntityId = existingSetting?.Id,
            Details = existingSetting is null
                ? $"Created system setting '{key}'."
                : $"Updated system setting '{key}'.",
            OccurredAtUtc = DateTime.UtcNow
        };

        await _systemSettingRepository.UpsertWithAuditAsync(
            setting,
            auditLog);
    }


    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Setting key is required.",
                nameof(key));
        }

        if (key.Length > 100)
        {
            throw new ArgumentException(
                "Setting key cannot exceed 100 characters.",
                nameof(key));
        }
    }

    private static void ValidateValue(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length > 2000)
        {
            throw new ArgumentException(
                "Setting value cannot exceed 2000 characters.",
                nameof(value));
        }
    }
}
