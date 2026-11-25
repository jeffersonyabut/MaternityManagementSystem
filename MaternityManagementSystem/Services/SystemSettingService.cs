
using System.Globalization;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public sealed class SystemSettingService
{
    public const int MinimumAppointmentDurationMinutes = 5;
    public const int MaximumAppointmentDurationMinutes = 240;
    public const int DefaultAppointmentDurationMinutes = 30;

    private static readonly HashSet<string> KnownSettingKeys =
    [
        SystemSettingKeys.Clinic.Name,
        SystemSettingKeys.Clinic.LogoPath,
        SystemSettingKeys.Clinic.Address,
        SystemSettingKeys.Clinic.ContactNumber,
        SystemSettingKeys.Clinic.Email,
        SystemSettingKeys.Appointments.DefaultDuration,
        SystemSettingKeys.Notifications.SmsEnabled,
        SystemSettingKeys.Notifications.AppointmentReminders,
        SystemSettingKeys.Regional.TimeZone,
        SystemSettingKeys.Regional.DateFormat
    ];

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
        ArgumentNullException.ThrowIfNull(value);

        await SaveBatchAsync(
            new Dictionary<string, string>
            {
                [key] = value
            });
    }

    public async Task SaveBatchAsync(
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        await _authorizationService.EnsureAdminAsync();

        if (values.Count == 0)
        {
            return;
        }

        var validatedValues = new Dictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var (key, originalValue) in values)
        {
            ValidateKey(key);
            ValidateValue(originalValue);

            string value = originalValue.Trim();

            ValidateSettingValue(key, value);

            if (!validatedValues.TryAdd(key, value))
            {
                throw new ArgumentException(
                    $"Duplicate system setting key '{key}'.",
                    nameof(values));
            }
        }

        var currentUser = _sessionService.CurrentUser
            ?? throw new UnauthorizedAccessException(
                "You must be logged in to perform this action.");

        DateTime now = DateTime.UtcNow;

        var items =
            new List<(SystemSetting Setting, AuditLog AuditLog)>();

        foreach (var (key, value) in validatedValues)
        {
            var existingSetting =
                await _systemSettingRepository.GetByKeyAsync(key);

            var setting = new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAtUtc = now,
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
                OccurredAtUtc = now
            };

            items.Add((setting, auditLog));
        }

        await _systemSettingRepository.UpsertManyWithAuditAsync(items);
    }

    private static void ValidateSettingValue(
        string key,
        string value)
    {
        if (key == SystemSettingKeys.Appointments.DefaultDuration)
        {
            if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int duration) ||
                duration < MinimumAppointmentDurationMinutes ||
                duration > MaximumAppointmentDurationMinutes)
            {
                throw new ArgumentException(
                    $"Default appointment duration must be between " +
                    $"{MinimumAppointmentDurationMinutes} and " +
                    $"{MaximumAppointmentDurationMinutes} minutes.");
            }
        }

        if (key == SystemSettingKeys.Notifications.SmsEnabled ||
            key == SystemSettingKeys.Notifications.AppointmentReminders)
        {
            if (!bool.TryParse(value, out _))
            {
                throw new ArgumentException(
                    $"Setting '{key}' must be either true or false.");
            }
        }

        if (key == SystemSettingKeys.Regional.TimeZone)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Time zone is required.");
            }

            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(value);
            }
            catch (TimeZoneNotFoundException)
            {
                throw new ArgumentException(
                    $"The time zone '{value}' is not supported.");
            }
            catch (InvalidTimeZoneException)
            {
                throw new ArgumentException(
                    $"The time zone '{value}' is invalid.");
            }
        }

        if (key == SystemSettingKeys.Regional.DateFormat)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > 50)
            {
                throw new ArgumentException(
                    "Date format must contain between 1 and 50 characters.");
            }

            try
            {
                _ = DateTime.Now.ToString(
                    value,
                    CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new ArgumentException(
                    "The specified date format is invalid.");
            }
        }
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

        if (!KnownSettingKeys.Contains(key))
        {
            throw new ArgumentException(
                $"Unknown system setting key: '{key}'.",
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
                "Setting value cannot exceed 2000 characters.");
        }
    }
}
