
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public class PregnancyRepository
{

    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public PregnancyRepository()
    {
    }

    public PregnancyRepository(
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

    public async Task<Pregnancy> AddAsync(
        Pregnancy pregnancy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);



        await using var dbContext = await CreateDbContextAsync(
        cancellationToken);

        dbContext.Pregnancies.Add(pregnancy);

        await dbContext.SaveChangesAsync(cancellationToken);

        return pregnancy;
    }

    public async Task<List<Pregnancy>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        await using var dbContext = await CreateDbContextAsync(
        cancellationToken);

        return await dbContext.Pregnancies
            .AsNoTracking()
            .Where(pregnancy => pregnancy.PatientId == patientId)
            .OrderByDescending(pregnancy => pregnancy.CreatedAtUtc)
            .ThenByDescending(pregnancy => pregnancy.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Pregnancy?> GetByIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        if (pregnancyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancyId));
        }


        await using var dbContext = await CreateDbContextAsync(
        cancellationToken);

        return await dbContext.Pregnancies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                pregnancy => pregnancy.Id == pregnancyId,
                cancellationToken);
    }

    public async Task UpdateAsync(
        Pregnancy pregnancy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        await using var dbContext = await CreateDbContextAsync(
        cancellationToken);

        bool pregnancyExists = await dbContext.Pregnancies
            .AnyAsync(
                existing => existing.Id == pregnancy.Id,
                cancellationToken);

        if (!pregnancyExists)
        {
            throw new KeyNotFoundException(
                $"Pregnancy with ID {pregnancy.Id} was not found.");
        }

        dbContext.Pregnancies.Update(pregnancy);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
