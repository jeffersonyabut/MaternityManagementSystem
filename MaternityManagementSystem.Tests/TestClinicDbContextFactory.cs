
using System;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MaternityManagementSystem.Tests;

public sealed class TestClinicDbContextFactory
    : IDbContextFactory<ClinicDbContext>
{
    private readonly DbContextOptions<ClinicDbContext> _options;

    public TestClinicDbContextFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _options = new DbContextOptionsBuilder<ClinicDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
    }

    public ClinicDbContext CreateDbContext()
    {
        return new ClinicDbContext(_options);
    }

    public Task<ClinicDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(CreateDbContext());
    }
}
