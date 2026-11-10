
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public class PregnancyService
{
    private readonly PregnancyRepository _pregnancyRepository;
    private readonly PatientRepository _patientRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly SessionService _sessionService;

    public PregnancyService(
        PregnancyRepository pregnancyRepository,
        PatientRepository patientRepository,
        AuthorizationService authorizationService,
        SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(pregnancyRepository);
        ArgumentNullException.ThrowIfNull(patientRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _pregnancyRepository = pregnancyRepository;
        _patientRepository = patientRepository;
        _authorizationService = authorizationService;
        _sessionService = sessionService;
    }

    public async Task<Pregnancy> CreatePregnancyAsync(
        Pregnancy pregnancy,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(pregnancy);

        if (pregnancy.PatientId <= 0)
        {
            throw new ArgumentException(
                "A valid patient must be selected.",
                nameof(pregnancy));
        }

        var patient = await _patientRepository.GetByIdAsync(
            pregnancy.PatientId,
            cancellationToken);

        if (patient is null)
        {
            throw new ArgumentException(
                "The selected patient could not be found.",
                nameof(pregnancy));
        }

        NormalizePregnancy(pregnancy);
        PregnancyValidator.Validate(pregnancy);

        var now = DateTime.UtcNow;

        pregnancy.CreatedAtUtc = now;
        pregnancy.UpdatedAtUtc = null;

        var user = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = user.StaffId,
            Username = user.Username,
            Action = "Created",
            EntityType = "Pregnancy",
            Details = "Pregnancy record registered.",
            OccurredAtUtc = now
        };

        return await _pregnancyRepository.AddAsync(
            pregnancy,
            auditLog,
            cancellationToken);
    }

    public async Task<List<Pregnancy>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        return await _pregnancyRepository.GetByPatientIdAsync(
            patientId,
            cancellationToken);
    }

    public async Task<Pregnancy?> GetByIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        return await _pregnancyRepository.GetByIdAsync(
            pregnancyId,
            cancellationToken);
    }

    public async Task<List<Pregnancy>> GetForReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        string? searchTerm,
        string? status,
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

        if (!string.IsNullOrWhiteSpace(status))
        {
            string normalizedStatus = status.Trim();

            string[] allowedStatuses =
            [
                "Current",
                "Completed",
                "Other"
            ];

            if (!normalizedStatus.Equals(
                    "All",
                    StringComparison.OrdinalIgnoreCase) &&
                !allowedStatuses.Contains(
                    normalizedStatus,
                    StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Invalid pregnancy status.",
                    nameof(status));
            }
        }

        return await _pregnancyRepository.GetForReportAsync(
            startDate,
            endDate,
            searchTerm,
            status,
            cancellationToken);
    }

    public async Task<int> GetPregnancyRegistrationCountForClinicActivityReportAsync(
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

        return await _pregnancyRepository
            .GetPregnancyCountForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<List<DateTime>> GetPregnancyRegistrationDatesForClinicActivityReportAsync(
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

        return await _pregnancyRepository
            .GetRegistrationDatesForClinicActivityReportAsync(
                startDate,
                endDate,
                cancellationToken);
    }

    public async Task<Pregnancy> UpdatePregnancyAsync(
        Pregnancy updatedPregnancy,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(updatedPregnancy);

        if (updatedPregnancy.Id <= 0)
        {
            throw new ArgumentException(
                "A valid pregnancy ID is required.",
                nameof(updatedPregnancy));
        }

        var existingPregnancy = await _pregnancyRepository.GetByIdAsync(
            updatedPregnancy.Id,
            cancellationToken);

        if (existingPregnancy is null)
        {
            throw new InvalidOperationException(
                "The pregnancy record could not be found.");
        }

        if (existingPregnancy.PatientId != updatedPregnancy.PatientId)
        {
            throw new InvalidOperationException(
                "A pregnancy record cannot be reassigned to another patient.");
        }

        NormalizePregnancy(updatedPregnancy);
        PregnancyValidator.Validate(updatedPregnancy);

        var hasChanges =
            existingPregnancy.Status != updatedPregnancy.Status ||
            existingPregnancy.LastMenstrualPeriod !=
                updatedPregnancy.LastMenstrualPeriod ||
            existingPregnancy.EstimatedDueDate !=
                updatedPregnancy.EstimatedDueDate ||
            existingPregnancy.PregnancyEndDate !=
                updatedPregnancy.PregnancyEndDate ||
            existingPregnancy.PregnancyOutcome !=
                updatedPregnancy.PregnancyOutcome ||
            existingPregnancy.DeliveryMethod !=
                updatedPregnancy.DeliveryMethod ||
            existingPregnancy.GestationalAgeAtEndWeeks !=
                updatedPregnancy.GestationalAgeAtEndWeeks ||
            existingPregnancy.GestationalAgeAtEndDays !=
                updatedPregnancy.GestationalAgeAtEndDays ||
            existingPregnancy.NumberOfInfants !=
                updatedPregnancy.NumberOfInfants ||
            existingPregnancy.LivingChildrenCount !=
                updatedPregnancy.LivingChildrenCount ||
            existingPregnancy.Complications !=
                updatedPregnancy.Complications ||
            existingPregnancy.Notes != updatedPregnancy.Notes;

        if (!hasChanges)
        {
            return existingPregnancy;
        }

        var now = DateTime.UtcNow;

        existingPregnancy.Status = updatedPregnancy.Status;
        existingPregnancy.LastMenstrualPeriod =
            updatedPregnancy.LastMenstrualPeriod;
        existingPregnancy.EstimatedDueDate =
            updatedPregnancy.EstimatedDueDate;
        existingPregnancy.PregnancyEndDate =
            updatedPregnancy.PregnancyEndDate;
        existingPregnancy.PregnancyOutcome =
            updatedPregnancy.PregnancyOutcome;
        existingPregnancy.DeliveryMethod =
            updatedPregnancy.DeliveryMethod;
        existingPregnancy.GestationalAgeAtEndWeeks =
            updatedPregnancy.GestationalAgeAtEndWeeks;
        existingPregnancy.GestationalAgeAtEndDays =
            updatedPregnancy.GestationalAgeAtEndDays;
        existingPregnancy.NumberOfInfants =
            updatedPregnancy.NumberOfInfants;
        existingPregnancy.LivingChildrenCount =
            updatedPregnancy.LivingChildrenCount;
        existingPregnancy.Complications =
            updatedPregnancy.Complications;
        existingPregnancy.Notes = updatedPregnancy.Notes;
        existingPregnancy.UpdatedAtUtc = now;

        var user = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = user.StaffId,
            Username = user.Username,
            Action = "Updated",
            EntityType = "Pregnancy",
            EntityId = existingPregnancy.Id,
            Details = "Pregnancy record updated.",
            OccurredAtUtc = now
        };

        await _pregnancyRepository.UpdateAsync(
            existingPregnancy,
            auditLog,
            cancellationToken);

        return existingPregnancy;
    }

    private static void NormalizePregnancy(Pregnancy pregnancy)
    {
        pregnancy.Status = pregnancy.Status?.Trim() ?? string.Empty;
        pregnancy.PregnancyOutcome =
            NormalizeText(pregnancy.PregnancyOutcome);
        pregnancy.DeliveryMethod =
            NormalizeText(pregnancy.DeliveryMethod);
        pregnancy.Complications =
            NormalizeText(pregnancy.Complications);
        pregnancy.Notes =
            NormalizeText(pregnancy.Notes);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
