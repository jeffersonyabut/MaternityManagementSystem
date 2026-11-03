
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public class PrenatalVisitService
{
    private readonly PrenatalVisitRepository _prenatalVisitRepository;
    private readonly PregnancyRepository _pregnancyRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly SessionService _sessionService;

    public PrenatalVisitService(
        PrenatalVisitRepository prenatalVisitRepository,
        PregnancyRepository pregnancyRepository,
        AuthorizationService authorizationService,
        SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisitRepository);
        ArgumentNullException.ThrowIfNull(pregnancyRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _prenatalVisitRepository = prenatalVisitRepository;
        _pregnancyRepository = pregnancyRepository;
        _authorizationService = authorizationService;
        _sessionService = sessionService;
    }

    public async Task<PrenatalVisit> CreateVisitAsync(
        PrenatalVisit prenatalVisit,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        ArgumentNullException.ThrowIfNull(prenatalVisit);

        if (prenatalVisit.PregnancyId <= 0)
        {
            throw new ArgumentException(
                "A valid pregnancy record must be selected.",
                nameof(prenatalVisit));
        }

        var pregnancy = await _pregnancyRepository.GetByIdAsync(
            prenatalVisit.PregnancyId,
            cancellationToken);

        if (pregnancy is null)
        {
            throw new ArgumentException(
                "The selected pregnancy record could not be found.",
                nameof(prenatalVisit));
        }

        PrenatalVisitValidator.Validate(prenatalVisit);

        var now = DateTime.UtcNow;

        prenatalVisit.Notes = NormalizeNotes(prenatalVisit.Notes);
        prenatalVisit.CreatedAtUtc = now;
        prenatalVisit.UpdatedAtUtc = null;

        var user = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = user.StaffId,
            Username = user.Username,
            Action = "Created",
            EntityType = "PrenatalVisit",
            Details = "Prenatal visit record registered.",
            OccurredAtUtc = now
        };

        return await _prenatalVisitRepository.AddAsync(
            prenatalVisit,
            auditLog,
            cancellationToken);
    }

    public async Task<List<PrenatalVisit>> GetByPregnancyIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        return await _prenatalVisitRepository.GetByPregnancyIdAsync(
            pregnancyId,
            cancellationToken);
    }

    public async Task<PrenatalVisit?> GetByIdAsync(
        int visitId,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        return await _prenatalVisitRepository.GetByIdAsync(
            visitId,
            cancellationToken);
    }

    public async Task<PrenatalVisit?> GetForReportByIdAsync(
    int visitId,
    CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        if (visitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitId));
        }

        return await _prenatalVisitRepository.GetForReportByIdAsync(
            visitId,
            cancellationToken);
    }

    public async Task<int> GetPrenatalVisitCountForClinicActivityReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _prenatalVisitRepository
            .GetPrenatalVisitCountForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }


    public async Task<List<DateOnly>> GetPrenatalVisitDatesForClinicActivityReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _prenatalVisitRepository
            .GetVisitDatesForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<List<int>> GetPatientIdsForClinicActivityReportAsync(
    DateOnly? startDate,
    DateOnly? endDate,
    CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        return await _prenatalVisitRepository
            .GetPatientIdsForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }


    public async Task<PrenatalVisit> UpdateVisitAsync(
        PrenatalVisit updatedVisit,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        ArgumentNullException.ThrowIfNull(updatedVisit);

        if (updatedVisit.Id <= 0)
        {
            throw new ArgumentException(
                "A valid prenatal visit ID is required.",
                nameof(updatedVisit));
        }

        var existingVisit = await _prenatalVisitRepository.GetByIdAsync(
            updatedVisit.Id,
            cancellationToken);

        if (existingVisit is null)
        {
            throw new InvalidOperationException(
                "The prenatal visit record could not be found.");
        }

        if (existingVisit.PregnancyId != updatedVisit.PregnancyId)
        {
            throw new InvalidOperationException(
                "A prenatal visit cannot be reassigned to another pregnancy.");
        }

        PrenatalVisitValidator.Validate(updatedVisit);

        var now = DateTime.UtcNow;

        existingVisit.VisitDate = updatedVisit.VisitDate;
        existingVisit.Notes = NormalizeNotes(updatedVisit.Notes);
        existingVisit.UpdatedAtUtc = now;

        var user = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = user.StaffId,
            Username = user.Username,
            Action = "Updated",
            EntityType = "PrenatalVisit",
            EntityId = existingVisit.Id,
            Details = "Prenatal visit record updated.",
            OccurredAtUtc = now
        };

        await _prenatalVisitRepository.UpdateAsync(
            existingVisit,
            auditLog,
            cancellationToken);

        return existingVisit;
    }

    private static string? NormalizeNotes(string? notes)
    {
        return string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();
    }
}
