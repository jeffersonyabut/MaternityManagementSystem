
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public class AuditLogService
{
    private readonly AuditLogRepository _auditLogRepository;
    private readonly SessionService _sessionService;
    private readonly AuthorizationService _authorizationService;

    public AuditLogService(
        AuditLogRepository auditLogRepository,
        SessionService sessionService,
        AuthorizationService authorizationService)
    {
        ArgumentNullException.ThrowIfNull(auditLogRepository);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(authorizationService);

        _auditLogRepository = auditLogRepository;
        _sessionService = sessionService;
        _authorizationService = authorizationService;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        int? entityId = null,
        string? details = null,
        string? reason = null)
    {
        var user = _sessionService.CurrentUser;

        var auditLog = new AuditLog
        {
            StaffId = user?.StaffId,
            Username = user?.Username ?? "System",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Reason = reason,
            OccurredAtUtc = DateTime.UtcNow
        };

        await _auditLogRepository.AddAsync(auditLog);
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count = 100)
    {
        await _authorizationService.EnsureAdminAsync();

        if (count < 1 || count > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Audit log count must be between 1 and 500.");
        }

        return await _auditLogRepository.GetRecentAsync(count);
    }

    public async Task<List<AuditLog>> GetByEntityAsync(
        string entityType,
        int entityId)
    {
        await _authorizationService.EnsureAdminAsync();

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException(
                "Entity type is required.",
                nameof(entityType));
        }

        if (entityId <= 0)
        {
            throw new ArgumentException(
                "Invalid entity ID.",
                nameof(entityId));
        }

        return await _auditLogRepository.GetByEntityAsync(
            entityType.Trim(),
            entityId);
    }

    public async Task<AuditLogPage> GetPageAsync(
        AuditLogQuery query)
    {
        await _authorizationService.EnsureAdminAsync();

        ArgumentNullException.ThrowIfNull(query);

        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.Page),
                "Page number must be at least 1.");
        }

        if (query.PageSize != 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.PageSize),
                "Audit log page size must be exactly 10.");
        }

        if (query.StartDateUtc.HasValue &&
            query.EndDateUtc.HasValue &&
            query.StartDateUtc.Value > query.EndDateUtc.Value)
        {
            throw new ArgumentException(
                "Start date cannot be later than end date.");
        }

        return await _auditLogRepository.GetPageAsync(query);
    }

    public async Task<AuditLogSummary> GetSummaryAsync()
    {
        await _authorizationService.EnsureAdminAsync();

        TimeZoneInfo philippineTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

        DateTime todayInPhilippineTime =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                philippineTimeZone).Date;

        DateTime tomorrowInPhilippineTime =
            todayInPhilippineTime.AddDays(1);

        DateTime startOfTodayUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(
                    todayInPhilippineTime,
                    DateTimeKind.Unspecified),
                philippineTimeZone);

        DateTime startOfTomorrowUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(
                    tomorrowInPhilippineTime,
                    DateTimeKind.Unspecified),
                philippineTimeZone);

        return await _auditLogRepository.GetSummaryAsync(
            startOfTodayUtc,
            startOfTomorrowUtc);
    }


    public async Task<IReadOnlyList<string>> GetAvailableActionsAsync()
    {
        await _authorizationService.EnsureAdminAsync();

        return await _auditLogRepository.GetAvailableActionsAsync();
    }

    public async Task<IReadOnlyList<string>> GetAvailableEntityTypesAsync()
    {
        await _authorizationService.EnsureAdminAsync();

        return await _auditLogRepository.GetAvailableEntityTypesAsync();
    }


}
