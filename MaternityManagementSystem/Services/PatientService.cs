
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Sessions;
using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services;

public class PatientService
{
    private readonly PatientRepository _patientRepository;
    private readonly AuthorizationService _authorizationService;
    private readonly SessionService _sessionService;
    private readonly RegionalSettingsService? _regionalSettingsService;
    private readonly AppointmentSmsReminderService? _reminderService;

    public PatientService(
        PatientRepository patientRepository,
        AuthorizationService authorizationService,
        SessionService sessionService,
        AppointmentSmsReminderService? reminderService = null)
    {
        ArgumentNullException.ThrowIfNull(patientRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _patientRepository = patientRepository;
        _authorizationService = authorizationService;
        _sessionService = sessionService;
        _reminderService = reminderService;
    }

    public PatientService(
        PatientRepository patientRepository,
        AuthorizationService authorizationService,
        SessionService sessionService,
        RegionalSettingsService regionalSettingsService,
        AppointmentSmsReminderService? reminderService = null)
        : this(
            patientRepository,
            authorizationService,
            sessionService,
            reminderService)
    {
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        _regionalSettingsService = regionalSettingsService;
    }

    public async Task<Patient> RegisterPatientAsync(
        Patient patient,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();
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
        patient.SmsReminderConsentGeneration = 0;

        var currentUser = _sessionService.CurrentUser;

        if (currentUser is null)
        {
            throw new UnauthorizedAccessException(
                "You must be logged in to register a patient.");
        }

        if (patient.HasSmsReminderConsent)
        {
            // A newly registered patient begins at generation 0; an explicit
            // grant during registration is the first consent state change.
            patient.SmsReminderConsentGeneration = 1;
            patient.SmsReminderConsentRecordedAtUtc = DateTime.UtcNow;
            patient.SmsReminderConsentRecordedByStaffId =
                currentUser.StaffId;
        }
        else
        {
            patient.SmsReminderConsentRecordedAtUtc = null;
            patient.SmsReminderConsentRecordedByStaffId = null;
        }

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Created",
            EntityType = "Patient",
            Details = patient.HasSmsReminderConsent
                ? "Patient record registered with SMS appointment reminder consent."
                : "Patient record registered without SMS appointment reminder consent."
        };

        return await _patientRepository.AddAsync(
            patient,
            auditLog,
            cancellationToken);
    }

    public async Task<Patient> UpdatePatientAsync(
        Patient patient,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();
        ArgumentNullException.ThrowIfNull(patient);

        if (patient.Id <= 0)
        {
            throw new ArgumentException(
                "A valid patient ID is required.",
                nameof(patient));
        }

        ValidatePatient(patient);

        Patient? existingPatient =
            await _patientRepository.GetByIdAsync(
                patient.Id,
                cancellationToken);

        if (existingPatient is null)
        {
            throw new InvalidOperationException(
                "The patient record could not be found.");
        }

        bool contactNumberChanged =
            existingPatient.ContactNumber !=
            NormalizeOptionalText(patient.ContactNumber);

        bool hasChanges =
            existingPatient.FirstName != patient.FirstName.Trim() ||
            existingPatient.MiddleName != NormalizeOptionalText(patient.MiddleName) ||
            existingPatient.LastName != patient.LastName.Trim() ||
            existingPatient.DateOfBirth != patient.DateOfBirth ||
            existingPatient.Sex != NormalizeOptionalText(patient.Sex) ||
            existingPatient.CivilStatus != NormalizeOptionalText(patient.CivilStatus) ||
            existingPatient.ContactNumber != NormalizeOptionalText(patient.ContactNumber) ||
            existingPatient.Email != NormalizeOptionalText(patient.Email) ||
            existingPatient.Address != NormalizeOptionalText(patient.Address) ||
            existingPatient.EmergencyContactName != NormalizeOptionalText(patient.EmergencyContactName) ||
            existingPatient.EmergencyContactNumber != NormalizeOptionalText(patient.EmergencyContactNumber);

        if (!hasChanges)
        {
            return existingPatient;
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

        var currentUser = _sessionService.CurrentUser;

        if (currentUser is null)
        {
            throw new UnauthorizedAccessException(
                "You must be logged in to update a patient.");
        }

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = "Updated",
            EntityType = "Patient",
            Details = "Patient record updated."
        };

        await _patientRepository.UpdateAsync(
            existingPatient,
            auditLog,
            cancellationToken);

        if (contactNumberChanged)
        {
            await TryReconcilePatientRemindersAsync(
                patient.Id,
                cancellationToken);
        }

        return existingPatient;
    }

    public async Task<Patient> SetSmsReminderConsentAsync(
        int patientId,
        bool hasConsent,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        var currentUser = _sessionService.CurrentUser;

        if (currentUser is null)
        {
            throw new UnauthorizedAccessException(
                "You must be logged in to record patient consent.");
        }

        DateTime recordedAtUtc = DateTime.UtcNow;
        string action = hasConsent
            ? "SMS Reminder Consent Granted"
            : "SMS Reminder Consent Withdrawn";

        var auditLog = new AuditLog
        {
            StaffId = currentUser.StaffId,
            Username = currentUser.Username,
            Action = action,
            EntityType = "Patient",
            EntityId = patientId,
            Details = hasConsent
                ? "SMS appointment reminder consent granted."
                : "SMS appointment reminder consent withdrawn."
        };

        Patient updatedPatient = await _patientRepository.UpdateSmsReminderConsentAsync(
            patientId,
            hasConsent,
            recordedAtUtc,
            currentUser.StaffId,
            auditLog,
            cancellationToken);

        await TryReconcilePatientRemindersAsync(
            patientId,
            cancellationToken,
            recordedAtUtc);

        return updatedPatient;
    }

    private async Task TryReconcilePatientRemindersAsync(
        int patientId,
        CancellationToken cancellationToken,
        DateTime? nowUtc = null)
    {
        if (_reminderService is null)
        {
            return;
        }

        try
        {
            await _reminderService.ReconcilePatientAsync(
                patientId,
                nowUtc,
                cancellationToken);
        }
        catch (Exception)
        {
            // The patient change is already committed. A later startup pass
            // can recover reminder state without blocking consent/profile edits.
            System.Diagnostics.Debug.WriteLine(
                "Patient reminder reconciliation failed; pending work will be retried at startup.");
        }
    }

    public async Task<Patient?> GetPatientByIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        return await _patientRepository.GetByIdAsync(
            patientId,
            cancellationToken);
    }

    public async Task<Patient?> GetPatientByNumberAsync(
        string patientNumber,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentException.ThrowIfNullOrWhiteSpace(patientNumber);

        return await _patientRepository.GetByPatientNumberAsync(
            patientNumber,
            cancellationToken);
    }

    public async Task<List<Patient>> GetAllPatientsAsync(
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        return await _patientRepository.GetAllAsync(
            cancellationToken);
    }

    public async Task<List<Patient>> GetPatientMasterListAsync(
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        string? sex = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        return await _patientRepository.GetPatientMasterListAsync(
            startUtc,
            endUtc,
            sex,
            searchTerm,
            cancellationToken);
    }

    public async Task<List<Patient>> SearchPatientsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(searchTerm);

        return await _patientRepository.SearchAsync(
            searchTerm,
            cancellationToken);
    }

    public async Task<List<Patient>> SearchPatientsPageAsync(
        string searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(searchTerm);

        return await _patientRepository.SearchPageAsync(
            searchTerm,
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task<int> CountPatientSearchResultsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ArgumentNullException.ThrowIfNull(searchTerm);

        return await _patientRepository.CountSearchResultsAsync(
            searchTerm,
            cancellationToken);
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

    public async Task<IndividualPatientRecordReport?> GetIndividualPatientRecordReportAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        return await _patientRepository.GetIndividualPatientRecordReportAsync(
            patientId,
            cancellationToken);
    }

    public async Task<PrenatalCareReport> GetPrenatalCareReportAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        if (startDate.HasValue &&
            endDate.HasValue &&
            endDate.Value < startDate.Value)
        {
            throw new ArgumentException(
                "The end date cannot be earlier than the start date.");
        }

        return await _patientRepository.GetPrenatalCareReportAsync(
            startDate,
            endDate,
            searchTerm,
            cancellationToken);
    }

    public async Task<int> GetPatientRegistrationCountForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ValidateClinicActivityDateRange(startDate, endDate);

        var (startUtc, endUtcExclusive) =
            ConvertClinicActivityDateRangeToUtc(startDate, endDate);

        return await _patientRepository
            .GetRegistrationCountForClinicActivityReportAsync(
                startUtc,
                endUtcExclusive,
                cancellationToken);
    }

    public async Task<List<DateTime>> GetPatientRegistrationDatesForClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        ValidateClinicActivityDateRange(startDate, endDate);

        var (startUtc, endUtcExclusive) =
            ConvertClinicActivityDateRangeToUtc(startDate, endDate);

        return await _patientRepository
            .GetRegistrationDatesForClinicActivityReportAsync(
                startUtc,
                endUtcExclusive,
                cancellationToken);
    }

    private static void ValidateClinicActivityDateRange(
        DateOnly? startDate,
        DateOnly? endDate)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }
    }

    private (DateTime? StartUtc, DateTime? EndUtcExclusive)
        ConvertClinicActivityDateRangeToUtc(
            DateOnly? startDate,
            DateOnly? endDate)
    {
        if (_regionalSettingsService is null)
        {
            throw new InvalidOperationException(
                "Regional settings must be provided to generate Clinic Activity Report registration data.");
        }

        DateTime? startUtc = startDate.HasValue
            ? _regionalSettingsService.ConvertLocalToUtc(
                startDate.Value.ToDateTime(TimeOnly.MinValue))
            : null;

        DateTime? endUtcExclusive = endDate.HasValue
            ? _regionalSettingsService.ConvertLocalToUtc(
                endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue))
            : null;

        return (startUtc, endUtcExclusive);
    }
}
