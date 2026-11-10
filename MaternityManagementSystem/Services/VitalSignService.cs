
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Services;

public class VitalSignService
{
    private readonly VitalSignRepository _vitalSignRepository;
    private readonly PrenatalVisitRepository _prenatalVisitRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly SessionService _sessionService;

    public VitalSignService(
        VitalSignRepository vitalSignRepository,
        PrenatalVisitRepository prenatalVisitRepository,
        AuthorizationService authorizationService,
        SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(vitalSignRepository);
        ArgumentNullException.ThrowIfNull(prenatalVisitRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _vitalSignRepository = vitalSignRepository;
        _prenatalVisitRepository = prenatalVisitRepository;
        _authorizationService = authorizationService;
        _sessionService = sessionService;
    }

    public async Task<VitalSign> CreateVitalSignAsync(
        VitalSign vitalSign,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(vitalSign);

        if (vitalSign.PrenatalVisitId <= 0)
        {
            throw new ArgumentException(
                "A valid prenatal visit must be selected.",
                nameof(vitalSign));
        }

        var prenatalVisit = await _prenatalVisitRepository.GetByIdAsync(
            vitalSign.PrenatalVisitId,
            cancellationToken);

        if (prenatalVisit is null)
        {
            throw new ArgumentException(
                "The selected prenatal visit could not be found.",
                nameof(vitalSign));
        }

        vitalSign.Notes = NormalizeNotes(vitalSign.Notes);

        VitalSignValidator.Validate(vitalSign);

        vitalSign.CreatedAtUtc = DateTime.UtcNow;
        vitalSign.UpdatedAtUtc = null;

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Created",
            EntityType = "VitalSign",
            Details = "Vital signs record created."
        };

        return await _vitalSignRepository.AddAsync(
            vitalSign,
            auditLog,
            cancellationToken);
    }

    public async Task<VitalSign?> GetByIdAsync(
        int vitalSignId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        return await _vitalSignRepository.GetByIdAsync(
            vitalSignId,
            cancellationToken);
    }

    public async Task<List<VitalSign>> GetByPrenatalVisitIdAsync(
        int prenatalVisitId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        return await _vitalSignRepository.GetByPrenatalVisitIdAsync(
            prenatalVisitId,
            cancellationToken);
    }

    public async Task<VitalSign> UpdateVitalSignAsync(
        VitalSign updatedVitalSign,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(updatedVitalSign);

        if (updatedVitalSign.Id <= 0)
        {
            throw new ArgumentException(
                "A valid vital signs record ID is required.",
                nameof(updatedVitalSign));
        }

        var existingVitalSign = await _vitalSignRepository.GetByIdAsync(
            updatedVitalSign.Id,
            cancellationToken);

        if (existingVitalSign is null)
        {
            throw new InvalidOperationException(
                "The vital signs record could not be found.");
        }

        if (existingVitalSign.IsArchived)
        {
            throw new InvalidOperationException(
                "Archived vital signs records cannot be edited.");
        }

        if (existingVitalSign.PrenatalVisitId !=
            updatedVitalSign.PrenatalVisitId)
        {
            throw new InvalidOperationException(
                "A vital signs record cannot be reassigned to another prenatal visit.");
        }

        updatedVitalSign.Notes = NormalizeNotes(
            updatedVitalSign.Notes);

        VitalSignValidator.Validate(updatedVitalSign);

        existingVitalSign.SystolicBloodPressure =
            updatedVitalSign.SystolicBloodPressure;

        existingVitalSign.DiastolicBloodPressure =
            updatedVitalSign.DiastolicBloodPressure;

        existingVitalSign.HeartRate =
            updatedVitalSign.HeartRate;

        existingVitalSign.RespiratoryRate =
            updatedVitalSign.RespiratoryRate;

        existingVitalSign.TemperatureCelsius =
            updatedVitalSign.TemperatureCelsius;

        existingVitalSign.OxygenSaturation =
            updatedVitalSign.OxygenSaturation;

        existingVitalSign.WeightKg =
            updatedVitalSign.WeightKg;

        existingVitalSign.FetalHeartRate =
            updatedVitalSign.FetalHeartRate;

        existingVitalSign.RecordedAtUtc =
            updatedVitalSign.RecordedAtUtc;

        existingVitalSign.Notes =
            updatedVitalSign.Notes;

        existingVitalSign.UpdatedAtUtc = DateTime.UtcNow;

        var currentUser = _sessionService.CurrentUser!;

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Updated",
            EntityType = "VitalSign",
            EntityId = existingVitalSign.Id,
            Details = "Vital signs record updated."
        };

        await _vitalSignRepository.UpdateAsync(
            existingVitalSign,
            auditLog,
            cancellationToken);

        return existingVitalSign;
    }

    public async Task ArchiveVitalSignAsync(
        int vitalSignId,
        string? archiveReason,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (vitalSignId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vitalSignId));
        }

        if (string.IsNullOrWhiteSpace(archiveReason))
        {
            throw new ArgumentException(
                "An archive reason is required.",
                nameof(archiveReason));
        }

        var currentUser = _sessionService.CurrentUser!;

        var normalizedReason = archiveReason.Trim();

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Archived",
            EntityType = "VitalSign",
            EntityId = vitalSignId,
            Details = "Vital signs record archived.",
            Reason = normalizedReason
        };

        await _vitalSignRepository.ArchiveAsync(
            vitalSignId,
            normalizedReason,
            auditLog,
            cancellationToken);
    }

    private static string? NormalizeNotes(string? notes)
    {
        return string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();
    }
}
