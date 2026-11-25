
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MaternityManagementSystem.Data;

public sealed class ClinicDbContextFactory
    : IDbContextFactory<ClinicDbContext>
{
    public ClinicDbContext CreateDbContext()
    {
        return new ClinicDbContext();
    }

    public Task<ClinicDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(CreateDbContext());
    }
}
