
using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services.Reports;

public class PatientReportService
{
    private static readonly TimeZoneInfo ClinicTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

    private readonly PatientService _patientService;

    public PatientReportService(PatientService patientService)
    {
        ArgumentNullException.ThrowIfNull(patientService);

        _patientService = patientService;
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
            ? ConvertPhilippineDateToUtc(startDate.Value)
            : null;

        DateTime? endUtc = endDate.HasValue
            ? ConvertPhilippineDateToUtc(
                endDate.Value.AddDays(1))
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

    private static DateTime ConvertPhilippineDateToUtc(
        DateOnly date)
    {
        DateTime philippineLocalDate = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(
            philippineLocalDate,
            ClinicTimeZone);
    }
}
