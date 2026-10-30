
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public class AuditLogService
{
    private readonly AuditLogRepository _auditLogRepository;
    private readonly SessionService _sessionService;

    public AuditLogService(
        AuditLogRepository auditLogRepository,
        SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(auditLogRepository);
        ArgumentNullException.ThrowIfNull(sessionService);

        _auditLogRepository = auditLogRepository;
        _sessionService = sessionService;
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

    public Task<List<AuditLog>> GetRecentAsync(int count = 100)
    {
        return _auditLogRepository.GetRecentAsync(count);
    }

    public Task<List<AuditLog>> GetByEntityAsync(
        string entityType,
        int entityId)
    {
        return _auditLogRepository.GetByEntityAsync(entityType, entityId);
    }
}
