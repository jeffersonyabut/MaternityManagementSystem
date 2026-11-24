using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using System.Text.Json;
using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Repositories;

public class PatientRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public PatientRepository()
    {
    }

    public PatientRepository(
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

    public async Task<Patient> AddAsync(
        Patient patient,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Patients.Add(patient);

        // Save first so the database generates the patient ID.
        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.EntityId = patient.Id;
        auditLog.OccurredAtUtc = DateTime.UtcNow;

        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Commit both operations together.
        await transaction.CommitAsync(cancellationToken);

        return patient;
    }

    public async Task<Patient?> GetByIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {

        await using var dbContext =
        await CreateDbContextAsync(cancellationToken);

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

        await using var dbContext =
        await CreateDbContextAsync(cancellationToken);

        return await dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(
                patient => patient.PatientNumber == patientNumber,
                cancellationToken);
    }

    public async Task<List<Patient>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
        await CreateDbContextAsync(cancellationToken);

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

        await using var dbContext =
        await CreateDbContextAsync(cancellationToken);

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

        await using var dbContext =
        await CreateDbContextAsync(cancellationToken);

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

        await using var dbContext =
        await CreateDbContextAsync(cancellationToken);

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




    public async Task<List<Patient>> GetPatientMasterListAsync(
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        string? sex = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        if (startUtc.HasValue &&
            endUtc.HasValue &&
            endUtc.Value <= startUtc.Value)
        {
            throw new ArgumentException(
                "The end date must be later than the start date.");
        }

        string? normalizedSex =
            string.IsNullOrWhiteSpace(sex) ? null : sex.Trim();

        string normalizedSearch =
            searchTerm?.Trim() ?? string.Empty;

        string search = normalizedSearch.ToLowerInvariant();

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.Patients
            .AsNoTracking();

        // Registration date filter.
        if (startUtc.HasValue)
        {
            query = query.Where(patient =>
                patient.CreatedAtUtc >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            query = query.Where(patient =>
                patient.CreatedAtUtc < endUtc.Value);
        }

        // Sex filter.
        if (normalizedSex is not null)
        {
            query = query.Where(patient =>
                patient.Sex == normalizedSex);
        }

        // Patient number and full-name search.
        if (search.Length > 0)
        {
            query = query.Where(patient =>
                patient.PatientNumber.ToLower().Contains(search) ||
                patient.FirstName.ToLower().Contains(search) ||
                patient.LastName.ToLower().Contains(search) ||
                (
                    patient.MiddleName != null &&
                    patient.MiddleName.ToLower().Contains(search)
                ) ||
                (
                    (patient.FirstName + " " +
                     (patient.MiddleName ?? "") + " " +
                     patient.LastName)
                    .ToLower()
                    .Contains(search)
                ));
        }

        return await query
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .ThenBy(patient => patient.Id)
            .ToListAsync(cancellationToken);
    }




    public async Task UpdateAsync(
        Patient patient,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var existingPatient = await dbContext.Patients
            .FirstOrDefaultAsync(
                existing => existing.Id == patient.Id,
                cancellationToken);

        if (existingPatient is null)
        {
            throw new KeyNotFoundException(
                $"Patient with ID {patient.Id} was not found.");
        }

        // Capture the existing values before applying changes.
        var previousValues = new Dictionary<string, object?>
        {
            ["FirstName"] = existingPatient.FirstName,
            ["MiddleName"] = existingPatient.MiddleName,
            ["LastName"] = existingPatient.LastName,
            ["DateOfBirth"] = existingPatient.DateOfBirth,
            ["Sex"] = existingPatient.Sex,
            ["CivilStatus"] = existingPatient.CivilStatus,
            ["ContactNumber"] = existingPatient.ContactNumber,
            ["Email"] = existingPatient.Email,
            ["Address"] = existingPatient.Address,
            ["EmergencyContactName"] = existingPatient.EmergencyContactName,
            ["EmergencyContactNumber"] = existingPatient.EmergencyContactNumber
        };

        // Capture the submitted values.
        var updatedValues = new Dictionary<string, object?>
        {
            ["FirstName"] = patient.FirstName,
            ["MiddleName"] = patient.MiddleName,
            ["LastName"] = patient.LastName,
            ["DateOfBirth"] = patient.DateOfBirth,
            ["Sex"] = patient.Sex,
            ["CivilStatus"] = patient.CivilStatus,
            ["ContactNumber"] = patient.ContactNumber,
            ["Email"] = patient.Email,
            ["Address"] = patient.Address,
            ["EmergencyContactName"] = patient.EmergencyContactName,
            ["EmergencyContactNumber"] = patient.EmergencyContactNumber
        };

        // Keep only fields whose values actually changed.
        var changedPreviousValues = new Dictionary<string, object?>();
        var changedNewValues = new Dictionary<string, object?>();

        foreach (var field in previousValues.Keys)
        {
            if (!Equals(previousValues[field], updatedValues[field]))
            {
                changedPreviousValues[field] = previousValues[field];
                changedNewValues[field] = updatedValues[field];
            }
        }

        // Nothing meaningful changed. Do not create unnecessary history
        // or audit entries.
        if (changedPreviousValues.Count == 0)
        {
            return;
        }

        // Apply the submitted values to the tracked database entity.
        dbContext.Entry(existingPatient).CurrentValues.SetValues(patient);

        DateTime changedAtUtc = DateTime.UtcNow;

        auditLog.EntityId = existingPatient.Id;
        auditLog.OccurredAtUtc = changedAtUtc;

        // Create the before-and-after version.
        var recordVersion = new RecordVersion
        {
            EntityType = "Patient",
            EntityId = existingPatient.Id,

            PreviousValuesJson =
                JsonSerializer.Serialize(changedPreviousValues),

            NewValuesJson =
                JsonSerializer.Serialize(changedNewValues),

            StaffId = auditLog.StaffId,
            Username = auditLog.Username,
            ChangedAtUtc = changedAtUtc
        };

        // Save the patient update, audit log, and version history together.
        await dbContext.AuditLogs.AddAsync(
            auditLog,
            cancellationToken);

        await dbContext.RecordVersions.AddAsync(
            recordVersion,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }



    public async Task<IndividualPatientRecordReport?> GetIndividualPatientRecordReportAsync(
    int patientId,
    CancellationToken cancellationToken = default)
    {
        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        // Retrieve the patient.
        var patient = await dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == patientId,
                cancellationToken);

        if (patient is null)
        {
            return null;
        }

        // Retrieve all pregnancies belonging to the patient.
        var pregnancies = await dbContext.Pregnancies
            .AsNoTracking()
            .Where(p => p.PatientId == patientId)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var pregnancyIds = pregnancies
            .Select(p => p.Id)
            .ToList();

        // Retrieve prenatal visits belonging to those pregnancies.
        var prenatalVisits = await dbContext.PrenatalVisits
            .AsNoTracking()
            .Where(v => pregnancyIds.Contains(v.PregnancyId))
            .OrderBy(v => v.VisitDate)
            .ThenBy(v => v.Id)
            .ToListAsync(cancellationToken);

        var prenatalVisitIds = prenatalVisits
            .Select(v => v.Id)
            .ToList();

        // Retrieve non-archived vital signs.
        var vitalSigns = await dbContext.VitalSigns
            .AsNoTracking()
            .Where(v =>
                !v.IsArchived &&
                prenatalVisitIds.Contains(v.PrenatalVisitId))
            .OrderBy(v => v.RecordedAtUtc)
            .ThenBy(v => v.Id)
            .ToListAsync(cancellationToken);

        // Retrieve non-archived laboratory results.
        var laboratoryResults = await dbContext.LaboratoryResults
            .AsNoTracking()
            .Where(r =>
                r.PatientId == patientId &&
                !r.IsArchived)
            .OrderBy(r => r.TestDate)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return new IndividualPatientRecordReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Patient = patient,
            Pregnancies = pregnancies,
            PrenatalVisits = prenatalVisits,
            VitalSigns = vitalSigns,
            LaboratoryResults = laboratoryResults
        };
    }


    public async Task<PrenatalCareReport> GetPrenatalCareReportAsync(
    DateOnly? startDate = null,
    DateOnly? endDate = null,
    string? searchTerm = null,
    CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            endDate.Value < startDate.Value)
        {
            throw new ArgumentException(
                "The end date cannot be earlier than the start date.");
        }

        string search = searchTerm?.Trim().ToLowerInvariant() ?? string.Empty;

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        IQueryable<PrenatalVisit> query = dbContext.PrenatalVisits
            .AsNoTracking()
            .Include(visit => visit.Pregnancy)
                .ThenInclude(pregnancy => pregnancy.Patient)
            .Include(visit => visit.VitalSigns
                .Where(vitalSign => !vitalSign.IsArchived));

        // Filter by prenatal visit date.
        if (startDate.HasValue)
        {
            query = query.Where(visit =>
                visit.VisitDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(visit =>
                visit.VisitDate <= endDate.Value);
        }

        // Search by patient number or patient name.
        if (search.Length > 0)
        {
            query = query.Where(visit =>
                visit.Pregnancy.Patient.PatientNumber
                    .ToLower().Contains(search) ||

                visit.Pregnancy.Patient.FirstName
                    .ToLower().Contains(search) ||

                visit.Pregnancy.Patient.LastName
                    .ToLower().Contains(search) ||

                (visit.Pregnancy.Patient.MiddleName != null &&
                 visit.Pregnancy.Patient.MiddleName
                    .ToLower().Contains(search)) ||

                ((
                    visit.Pregnancy.Patient.FirstName + " " +
                    (visit.Pregnancy.Patient.MiddleName ?? "") + " " +
                    visit.Pregnancy.Patient.LastName
                ).ToLower().Contains(search))
            );
        }

        var prenatalVisits = await query
            .OrderBy(visit => visit.VisitDate)
            .ThenBy(visit => visit.Id)
            .ToListAsync(cancellationToken);

        return new PrenatalCareReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            SearchTerm = searchTerm?.Trim(),
            PrenatalVisits = prenatalVisits
        };
    }




    public async Task<int> GetRegistrationCountForClinicActivityReportAsync(
        DateTime? startUtc,
        DateTime? endUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        if (startUtc.HasValue &&
            endUtcExclusive.HasValue &&
            endUtcExclusive.Value <= startUtc.Value)
        {
            throw new ArgumentException(
                "The end date must be later than the start date.");
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.Patients
            .AsNoTracking()
            .AsQueryable();

        if (startUtc.HasValue)
        {
            query = query.Where(patient =>
                patient.CreatedAtUtc >= startUtc.Value);
        }

        if (endUtcExclusive.HasValue)
        {
            query = query.Where(patient =>
                patient.CreatedAtUtc < endUtcExclusive.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<DateTime>> GetRegistrationDatesForClinicActivityReportAsync(
        DateTime? startUtc,
        DateTime? endUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        if (startUtc.HasValue &&
            endUtcExclusive.HasValue &&
            endUtcExclusive.Value <= startUtc.Value)
        {
            throw new ArgumentException(
                "The end date must be later than the start date.");
        }

        await using var dbContext =
            await CreateDbContextAsync(cancellationToken);

        var query = dbContext.Patients
            .AsNoTracking()
            .AsQueryable();

        if (startUtc.HasValue)
        {
            query = query.Where(patient =>
                patient.CreatedAtUtc >= startUtc.Value);
        }

        if (endUtcExclusive.HasValue)
        {
            query = query.Where(patient =>
                patient.CreatedAtUtc < endUtcExclusive.Value);
        }

        return await query
            .OrderBy(patient => patient.CreatedAtUtc)
            .Select(patient => patient.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }



}
