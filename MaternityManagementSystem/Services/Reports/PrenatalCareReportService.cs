using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services.Reports;

public class PrenatalCareReportService
{
    private readonly PatientService _patientService;

    public PrenatalCareReportService(
        PatientService patientService)
    {
        ArgumentNullException.ThrowIfNull(patientService);

        _patientService = patientService;
    }

    public async Task<PrenatalCareReport> GeneratePrenatalCareReportAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
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

        return await _patientService.GetPrenatalCareReportAsync(
            startDate,
            endDate,
            searchTerm,
            cancellationToken);
    }
}
