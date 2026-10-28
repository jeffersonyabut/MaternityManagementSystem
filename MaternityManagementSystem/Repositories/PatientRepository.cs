using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public class PatientRepository
{
    public async Task<Patient> AddAsync(
        Patient patient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);

        await using var dbContext = new ClinicDbContext();

        dbContext.Patients.Add(patient);

        await dbContext.SaveChangesAsync(cancellationToken);

        return patient;
    }

    public async Task<Patient?> GetByIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new ClinicDbContext();

        return await dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(
                patient => patient.Id == patientId,
                cancellationToken);
    }

    public async Task<Patient?> GetByPatientNumberAsync(
        string patientNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patientNumber);

        await using var dbContext = new ClinicDbContext();

        return await dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(
                patient => patient.PatientNumber == patientNumber,
                cancellationToken);
    }

    public async Task<List<Patient>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new ClinicDbContext();

        return await dbContext.Patients
            .AsNoTracking()
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Patient>> SearchAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchTerm);

        string normalizedSearchTerm = searchTerm.Trim();

        await using var dbContext = new ClinicDbContext();

        if (normalizedSearchTerm.Length == 0)
        {
            return await dbContext.Patients
                .AsNoTracking()
                .OrderBy(patient => patient.LastName)
                .ThenBy(patient => patient.FirstName)
                .ToListAsync(cancellationToken);
        }

        return await dbContext.Patients
            .AsNoTracking()
            .Where(patient =>
                patient.PatientNumber.Contains(normalizedSearchTerm) ||
                patient.FirstName.Contains(normalizedSearchTerm) ||
                (patient.MiddleName != null &&
                 patient.MiddleName.Contains(normalizedSearchTerm)) ||
                patient.LastName.Contains(normalizedSearchTerm) ||
                (patient.ContactNumber != null &&
                 patient.ContactNumber.Contains(normalizedSearchTerm)))
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .ToListAsync(cancellationToken);
    }


    public async Task<List<Patient>> SearchPageAsync(
        string searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchTerm);

        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }


        string normalizedSearchTerm = searchTerm.Trim();
        string search = normalizedSearchTerm.ToLowerInvariant();


        await using var dbContext = new ClinicDbContext();

        var query = dbContext.Patients
            .AsNoTracking();

        if (normalizedSearchTerm.Length > 0)
        {
            query = query.Where(patient =>
            patient.PatientNumber.ToLower().Contains(search) ||
            patient.FirstName.ToLower().Contains(search) ||
            (patient.MiddleName != null &&
             patient.MiddleName.ToLower().Contains(search)) ||
            patient.LastName.ToLower().Contains(search) ||
            (patient.ContactNumber != null &&
             patient.ContactNumber.Contains(normalizedSearchTerm)));
        }

        return await query
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .ThenBy(patient => patient.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    }


    public async Task<int> CountSearchResultsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchTerm);

        string normalizedSearchTerm = searchTerm.Trim();
        string search = normalizedSearchTerm.ToLowerInvariant();

        await using var dbContext = new ClinicDbContext();

        if (normalizedSearchTerm.Length == 0)
        {
            return await dbContext.Patients
                .CountAsync(cancellationToken);
        }


        return await dbContext.Patients
            .Where(patient =>
                patient.PatientNumber.ToLower().Contains(search) ||
                patient.FirstName.ToLower().Contains(search) ||
                (patient.MiddleName != null &&
                 patient.MiddleName.ToLower().Contains(search)) ||
                patient.LastName.ToLower().Contains(search) ||
                (patient.ContactNumber != null &&
                 patient.ContactNumber.Contains(normalizedSearchTerm)))
            .CountAsync(cancellationToken);

    }



    public async Task UpdateAsync(
        Patient patient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);

        await using var dbContext = new ClinicDbContext();

        bool patientExists = await dbContext.Patients
            .AnyAsync(
                existingPatient => existingPatient.Id == patient.Id,
                cancellationToken);

        if (!patientExists)
        {
            throw new KeyNotFoundException(
                $"Patient with ID {patient.Id} was not found.");
        }

        dbContext.Patients.Update(patient);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
