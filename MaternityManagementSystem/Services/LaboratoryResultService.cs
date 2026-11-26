
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public class LaboratoryResultService
{
    private readonly LaboratoryResultRepository _laboratoryResultRepository;
    private readonly PatientRepository _patientRepository;
    private readonly PrenatalVisitRepository _prenatalVisitRepository;
    private readonly PregnancyRepository _pregnancyRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly SessionService _sessionService;

    public LaboratoryResultService(
        LaboratoryResultRepository laboratoryResultRepository,
        PatientRepository patientRepository,
        PrenatalVisitRepository prenatalVisitRepository,
        PregnancyRepository pregnancyRepository,
        AuthorizationService authorizationService,
        SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(laboratoryResultRepository);
        ArgumentNullException.ThrowIfNull(patientRepository);
        ArgumentNullException.ThrowIfNull(prenatalVisitRepository);
        ArgumentNullException.ThrowIfNull(pregnancyRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _laboratoryResultRepository = laboratoryResultRepository;
        _patientRepository = patientRepository;
        _prenatalVisitRepository = prenatalVisitRepository;
        _pregnancyRepository = pregnancyRepository;
        _authorizationService = authorizationService;
        _sessionService = sessionService;
    }

    public async Task<LaboratoryResult> CreateLaboratoryResultAsync(
        LaboratoryResult laboratoryResult,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(laboratoryResult);

        await ValidatePatientAndVisitAsync(
            laboratoryResult.PatientId,
            laboratoryResult.PrenatalVisitId,
            cancellationToken);

        NormalizeFields(laboratoryResult);

        laboratoryResult.RequestedDate ??=
            DateOnly.FromDateTime(DateTime.Today);

        LaboratoryResultValidator.Validate(laboratoryResult);

        laboratoryResult.CreatedAtUtc = DateTime.UtcNow;
        laboratoryResult.UpdatedAtUtc = null;

        laboratoryResult.IsArchived = false;
        laboratoryResult.ArchivedAtUtc = null;
        laboratoryResult.ArchiveReason = null;

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Created",
            EntityType = "LaboratoryResult",
            Details = $"Laboratory test record created with status {laboratoryResult.Status}."
        };

        return await _laboratoryResultRepository.AddAsync(
            laboratoryResult,
            auditLog,
            cancellationToken);
    }

    public async Task<LaboratoryResult?> GetByIdAsync(
        int laboratoryResultId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (laboratoryResultId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laboratoryResultId));
        }

        return await _laboratoryResultRepository.GetByIdAsync(
            laboratoryResultId,
            cancellationToken);
    }

    public async Task<List<LaboratoryResult>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        return await _laboratoryResultRepository.GetByPatientIdAsync(
            patientId,
            cancellationToken);
    }

    public async Task<List<LaboratoryResult>> GetByPrenatalVisitIdAsync(
        int prenatalVisitId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (prenatalVisitId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prenatalVisitId));
        }

        return await _laboratoryResultRepository.GetByPrenatalVisitIdAsync(
            prenatalVisitId,
            cancellationToken);
    }

    public async Task<List<LaboratoryResult>> GetForReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        string? searchTerm,
        string? testName,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _laboratoryResultRepository.GetForReportAsync(
            startDate,
            endDate,
            searchTerm,
            testName,
            cancellationToken);
    }

    public async Task<int> GetLaboratoryResultCountForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _laboratoryResultRepository
            .GetLaboratoryResultCountForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<List<DateOnly>> GetLaboratoryResultDatesForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _laboratoryResultRepository
            .GetTestDatesForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<List<int>> GetPatientIdsForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _laboratoryResultRepository
            .GetPatientIdsForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<LaboratoryResult> UpdateLaboratoryResultAsync(
        LaboratoryResult updatedLaboratoryResult,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(updatedLaboratoryResult);

        if (updatedLaboratoryResult.Id <= 0)
        {
            throw new ArgumentException(
                "A valid laboratory result ID is required.",
                nameof(updatedLaboratoryResult));
        }

        var existingLaboratoryResult =
            await _laboratoryResultRepository.GetByIdAsync(
                updatedLaboratoryResult.Id,
                cancellationToken);

        if (existingLaboratoryResult is null)
        {
            throw new InvalidOperationException(
                "The laboratory result record could not be found.");
        }

        if (existingLaboratoryResult.IsArchived)
        {
            throw new InvalidOperationException(
                "Archived laboratory results cannot be edited.");
        }

        if (existingLaboratoryResult.PatientId !=
            updatedLaboratoryResult.PatientId)
        {
            throw new InvalidOperationException(
                "A laboratory result cannot be reassigned to another patient.");
        }

        if (existingLaboratoryResult.PrenatalVisitId !=
            updatedLaboratoryResult.PrenatalVisitId)
        {
            throw new InvalidOperationException(
                "A laboratory result cannot be reassigned to another prenatal visit.");
        }

        NormalizeFields(updatedLaboratoryResult);

        LaboratoryResultValidator.Validate(updatedLaboratoryResult);

        existingLaboratoryResult.TestName =
            updatedLaboratoryResult.TestName;

        existingLaboratoryResult.Status =
            updatedLaboratoryResult.Status;

        existingLaboratoryResult.RequestedDate =
            updatedLaboratoryResult.RequestedDate;

        existingLaboratoryResult.TestDate =
            updatedLaboratoryResult.TestDate;

        existingLaboratoryResult.Result =
            updatedLaboratoryResult.Result;

        existingLaboratoryResult.Unit =
            updatedLaboratoryResult.Unit;

        existingLaboratoryResult.ReferenceRange =
            updatedLaboratoryResult.ReferenceRange;

        existingLaboratoryResult.Notes =
            updatedLaboratoryResult.Notes;

        existingLaboratoryResult.UpdatedAtUtc = DateTime.UtcNow;

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Updated",
            EntityType = "LaboratoryResult",
            EntityId = existingLaboratoryResult.Id,
            Details = "Laboratory result record updated."
        };

        await _laboratoryResultRepository.UpdateAsync(
            existingLaboratoryResult,
            auditLog,
            cancellationToken);

        return existingLaboratoryResult;
    }

    public async Task ArchiveLaboratoryResultAsync(
        int laboratoryResultId,
        string? archiveReason,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (laboratoryResultId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laboratoryResultId));
        }

        if (string.IsNullOrWhiteSpace(archiveReason))
        {
            throw new ArgumentException(
                "An archive reason is required.",
                nameof(archiveReason));
        }

        string normalizedReason = archiveReason.Trim();

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Archived",
            EntityType = "LaboratoryResult",
            EntityId = laboratoryResultId,
            Details = "Laboratory result record archived.",
            Reason = normalizedReason
        };

        await _laboratoryResultRepository.ArchiveAsync(
            laboratoryResultId,
            normalizedReason,
            auditLog,
            cancellationToken);
    }

    private async Task ValidatePatientAndVisitAsync(
        int patientId,
        int? prenatalVisitId,
        CancellationToken cancellationToken)
    {
        if (patientId <= 0)
        {
            throw new ArgumentException(
                "A valid patient must be selected.");
        }

        var patient = await _patientRepository.GetByIdAsync(
            patientId,
            cancellationToken);

        if (patient is null)
        {
            throw new ArgumentException(
                "The selected patient could not be found.");
        }

        if (prenatalVisitId is null)
        {
            return;
        }

        if (prenatalVisitId <= 0)
        {
            throw new ArgumentException(
                "A valid prenatal visit must be selected.");
        }

        var prenatalVisit = await _prenatalVisitRepository.GetByIdAsync(
            prenatalVisitId.Value,
            cancellationToken);

        if (prenatalVisit is null)
        {
            throw new ArgumentException(
                "The selected prenatal visit could not be found.");
        }

        var pregnancy = await _pregnancyRepository.GetByIdAsync(
            prenatalVisit.PregnancyId,
            cancellationToken);

        if (pregnancy is null)
        {
            throw new InvalidOperationException(
                "The pregnancy associated with the prenatal visit could not be found.");
        }

        if (pregnancy.PatientId != patientId)
        {
            throw new InvalidOperationException(
                "The selected prenatal visit does not belong to the selected patient.");
        }
    }

    private static void NormalizeFields(
        LaboratoryResult laboratoryResult)
    {
        laboratoryResult.TestName =
            laboratoryResult.TestName?.Trim() ?? string.Empty;

        laboratoryResult.Result = NormalizeOptionalText(
            laboratoryResult.Result);

        laboratoryResult.Unit =
            NormalizeOptionalText(laboratoryResult.Unit);

        laboratoryResult.ReferenceRange =
            NormalizeOptionalText(laboratoryResult.ReferenceRange);

        laboratoryResult.Notes =
            NormalizeOptionalText(laboratoryResult.Notes);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
