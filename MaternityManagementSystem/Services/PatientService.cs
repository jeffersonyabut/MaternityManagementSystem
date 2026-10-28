using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public class PatientService
{
    private readonly PatientRepository _patientRepository;

    public PatientService(PatientRepository patientRepository)
    {
        ArgumentNullException.ThrowIfNull(patientRepository);

        _patientRepository = patientRepository;
    }

    public async Task<Patient> RegisterPatientAsync(
        Patient patient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);

        ValidatePatient(patient);

        patient.PatientNumber =
            await GeneratePatientNumberAsync(cancellationToken);

        patient.FirstName = patient.FirstName.Trim();
        patient.MiddleName = NormalizeOptionalText(patient.MiddleName);
        patient.LastName = patient.LastName.Trim();
        patient.ContactNumber = NormalizeOptionalText(patient.ContactNumber);
        patient.Email = NormalizeOptionalText(patient.Email);
        patient.Address = NormalizeOptionalText(patient.Address);
        patient.EmergencyContactName =
            NormalizeOptionalText(patient.EmergencyContactName);
        patient.EmergencyContactNumber =
            NormalizeOptionalText(patient.EmergencyContactNumber);

        patient.CreatedAtUtc = DateTime.UtcNow;
        patient.UpdatedAtUtc = null;

        return await _patientRepository.AddAsync(
            patient,
            cancellationToken);
    }


    public async Task<Patient> UpdatePatientAsync(
        Patient patient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);

        if (patient.Id <= 0)
        {
            throw new ArgumentException(
                "A valid patient ID is required.",
                nameof(patient));
        }

        ValidatePatient(patient);

        // Load the existing record so immutable fields stay unchanged.
        Patient? existingPatient =
            await _patientRepository.GetByIdAsync(
                patient.Id,
                cancellationToken);

        if (existingPatient is null)
        {
            throw new InvalidOperationException(
                "The patient record could not be found.");
        }

        existingPatient.FirstName = patient.FirstName.Trim();
        existingPatient.MiddleName = NormalizeOptionalText(patient.MiddleName);
        existingPatient.LastName = patient.LastName.Trim();
        existingPatient.DateOfBirth = patient.DateOfBirth;
        existingPatient.Sex = NormalizeOptionalText(patient.Sex);
        existingPatient.CivilStatus = NormalizeOptionalText(patient.CivilStatus);
        existingPatient.ContactNumber = NormalizeOptionalText(patient.ContactNumber);
        existingPatient.Email = NormalizeOptionalText(patient.Email);
        existingPatient.Address = NormalizeOptionalText(patient.Address);
        existingPatient.EmergencyContactName =
            NormalizeOptionalText(patient.EmergencyContactName);
        existingPatient.EmergencyContactNumber =
            NormalizeOptionalText(patient.EmergencyContactNumber);

        existingPatient.UpdatedAtUtc = DateTime.UtcNow;

        await _patientRepository.UpdateAsync(
            existingPatient,
            cancellationToken);

        return existingPatient;
    }


    private static void ValidatePatient(Patient patient)
    {
        if (string.IsNullOrWhiteSpace(patient.FirstName))
        {
            throw new ArgumentException(
                "First name is required.",
                nameof(patient));
        }

        if (string.IsNullOrWhiteSpace(patient.LastName))
        {
            throw new ArgumentException(
                "Last name is required.",
                nameof(patient));
        }

        if (patient.FirstName.Trim().Length > 100)
        {
            throw new ArgumentException(
                "First name cannot exceed 100 characters.",
                nameof(patient));
        }

        if (patient.MiddleName?.Trim().Length > 100)
        {
            throw new ArgumentException(
                "Middle name cannot exceed 100 characters.",
                nameof(patient));
        }

        if (patient.LastName.Trim().Length > 100)
        {
            throw new ArgumentException(
                "Last name cannot exceed 100 characters.",
                nameof(patient));
        }

        if (patient.ContactNumber?.Trim().Length > 30)
        {
            throw new ArgumentException(
                "Contact number cannot exceed 30 characters.",
                nameof(patient));
        }

        if (patient.Email?.Trim().Length > 254)
        {
            throw new ArgumentException(
                "Email cannot exceed 254 characters.",
                nameof(patient));
        }

        if (patient.Address?.Trim().Length > 500)
        {
            throw new ArgumentException(
                "Address cannot exceed 500 characters.",
                nameof(patient));
        }

        if (patient.EmergencyContactName?.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Emergency contact name cannot exceed 200 characters.",
                nameof(patient));
        }

        if (patient.EmergencyContactNumber?.Trim().Length > 30)
        {
            throw new ArgumentException(
                "Emergency contact number cannot exceed 30 characters.",
                nameof(patient));
        }

        if (patient.DateOfBirth > DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ArgumentException(
                "Date of birth cannot be in the future.",
                nameof(patient));
        }
    }

    private async Task<string> GeneratePatientNumberAsync(
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 5;

        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string patientNumber =
                $"PAT-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

            Patient? existingPatient =
                await _patientRepository.GetByPatientNumberAsync(
                    patientNumber,
                    cancellationToken);

            if (existingPatient is null)
            {
                return patientNumber;
            }
        }

        throw new InvalidOperationException(
            "A unique patient number could not be generated. Please try again.");
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
