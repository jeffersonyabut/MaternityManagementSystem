using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public class RecordVersionService
{
    private readonly RecordVersionRepository _recordVersionRepository;
    private readonly AuthorizationService _authorizationService;

    public RecordVersionService(
        RecordVersionRepository recordVersionRepository,
        AuthorizationService authorizationService)
    {
        ArgumentNullException.ThrowIfNull(recordVersionRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);

        _recordVersionRepository = recordVersionRepository;
        _authorizationService = authorizationService;
    }

    public async Task<List<RecordVersion>> GetHistoryAsync(
        string entityType,
        int entityId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();


        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        if (entityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId));
        }

        return await _recordVersionRepository.GetByRecordAsync(
            entityType.Trim(),
            entityId,
            cancellationToken);
    }
}
