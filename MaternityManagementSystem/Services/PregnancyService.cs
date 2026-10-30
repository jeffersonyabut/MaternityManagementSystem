
using System;
using System.Collections.Generic;
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
        _authorizationService.EnsureAuthenticated();

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

        PregnancyValidator.Validate(pregnancy);

        var now = DateTime.UtcNow;

        pregnancy.Status = pregnancy.Status.Trim();
        pregnancy.Notes = NormalizeNotes(pregnancy.Notes);
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
        _authorizationService.EnsureAuthenticated();

        return await _pregnancyRepository.GetByPatientIdAsync(
            patientId,
            cancellationToken);
    }

    public async Task<Pregnancy?> GetByIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

        return await _pregnancyRepository.GetByIdAsync(
            pregnancyId,
            cancellationToken);
    }

    public async Task<Pregnancy> UpdatePregnancyAsync(
        Pregnancy updatedPregnancy,
        CancellationToken cancellationToken = default)
    {
        _authorizationService.EnsureAuthenticated();

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

        PregnancyValidator.Validate(updatedPregnancy);

        var now = DateTime.UtcNow;

        existingPregnancy.Status = updatedPregnancy.Status.Trim();
        existingPregnancy.LastMenstrualPeriod =
            updatedPregnancy.LastMenstrualPeriod;
        existingPregnancy.EstimatedDueDate =
            updatedPregnancy.EstimatedDueDate;
        existingPregnancy.PregnancyEndDate =
            updatedPregnancy.PregnancyEndDate;
        existingPregnancy.Notes =
            NormalizeNotes(updatedPregnancy.Notes);
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

    private static string? NormalizeNotes(string? notes)
    {
        return string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();
    }
}
