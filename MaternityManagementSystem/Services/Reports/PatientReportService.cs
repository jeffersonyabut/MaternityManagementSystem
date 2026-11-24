
using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services.Reports;

public class PatientReportService
{
    private readonly PatientService _patientService;
    private readonly RegionalSettingsService _regionalSettingsService;

    public PatientReportService(
        PatientService patientService,
        RegionalSettingsService regionalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(patientService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        _patientService = patientService;
        _regionalSettingsService = regionalSettingsService;
    }

    public async Task<PatientMasterListReport> GeneratePatientMasterListAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? sex = null,
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

        DateTime? startUtc = startDate.HasValue
            ? _regionalSettingsService.ConvertLocalToUtc(
                startDate.Value.ToDateTime(TimeOnly.MinValue))
            : null;

        DateTime? endUtc = endDate.HasValue
            ? _regionalSettingsService.ConvertLocalToUtc(
                endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue))
            : null;

        List<Models.Patient> patients =
            await _patientService.GetPatientMasterListAsync(
                startUtc,
                endUtc,
                sex,
                searchTerm,
                cancellationToken);

        return new PatientMasterListReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            Sex = string.IsNullOrWhiteSpace(sex) ? null : sex.Trim(),
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm)
                ? null
                : searchTerm.Trim(),
            Patients = patients
        };
    }
}
