
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public class RecordVersionRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public RecordVersionRepository()
    {
    }

    public RecordVersionRepository(
        IDbContextFactory<ClinicDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    private async Task<ClinicDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            return await _contextFactory.CreateDbContextAsync(
                cancellationToken);
        }

        return new ClinicDbContext();
    }

    public async Task<List<RecordVersion>> GetByRecordAsync(
        string entityType,
        int entityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        if (entityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId));
        }

        string normalizedEntityType = entityType.Trim();

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        return await dbContext.RecordVersions
            .AsNoTracking()
            .Where(version =>
                version.EntityType == normalizedEntityType &&
                version.EntityId == entityId)
            .OrderByDescending(version => version.ChangedAtUtc)
            .ThenByDescending(version => version.Id)
            .ToListAsync(cancellationToken);
    }
}
