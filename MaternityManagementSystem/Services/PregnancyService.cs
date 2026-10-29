
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public class PregnancyService
{
    private readonly PregnancyRepository _pregnancyRepository;
    private readonly PatientRepository _patientRepository;

    public PregnancyService(
        PregnancyRepository pregnancyRepository,
        PatientRepository patientRepository)
    {
        ArgumentNullException.ThrowIfNull(pregnancyRepository);
        ArgumentNullException.ThrowIfNull(patientRepository);

        _pregnancyRepository = pregnancyRepository;
        _patientRepository = patientRepository;
    }

    public async Task<Pregnancy> CreatePregnancyAsync(
        Pregnancy pregnancy,
        CancellationToken cancellationToken = default)
    {
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

        pregnancy.Status = pregnancy.Status.Trim();
        pregnancy.Notes = NormalizeNotes(pregnancy.Notes);
        pregnancy.CreatedAtUtc = DateTime.UtcNow;
        pregnancy.UpdatedAtUtc = null;

        return await _pregnancyRepository.AddAsync(
            pregnancy,
            cancellationToken);
    }

    public Task<List<Pregnancy>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        return _pregnancyRepository.GetByPatientIdAsync(
            patientId,
            cancellationToken);
    }

    public Task<Pregnancy?> GetByIdAsync(
        int pregnancyId,
        CancellationToken cancellationToken = default)
    {
        return _pregnancyRepository.GetByIdAsync(
            pregnancyId,
            cancellationToken);
    }

    public async Task<Pregnancy> UpdatePregnancyAsync(
        Pregnancy updatedPregnancy,
        CancellationToken cancellationToken = default)
    {
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
        existingPregnancy.Status = updatedPregnancy.Status.Trim();
        existingPregnancy.LastMenstrualPeriod =
            updatedPregnancy.LastMenstrualPeriod;
        existingPregnancy.EstimatedDueDate =
            updatedPregnancy.EstimatedDueDate;
        existingPregnancy.PregnancyEndDate =
            updatedPregnancy.PregnancyEndDate;
        existingPregnancy.Notes = NormalizeNotes(updatedPregnancy.Notes);
        existingPregnancy.UpdatedAtUtc = DateTime.UtcNow;

        await _pregnancyRepository.UpdateAsync(
            existingPregnancy,
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
