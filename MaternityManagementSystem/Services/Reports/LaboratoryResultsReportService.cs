using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.Services.Reports;

public class LaboratoryResultsReportService
{
    private readonly LaboratoryResultService
        _laboratoryResultService;

    public LaboratoryResultsReportService(
        LaboratoryResultService laboratoryResultService)
    {
        ArgumentNullException.ThrowIfNull(
            laboratoryResultService);

        _laboratoryResultService = laboratoryResultService;
    }

    public async Task<LaboratoryResultsReport>
        GenerateLaboratoryResultsReportAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            string? searchTerm,
            string? testName,
            CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        var results = await _laboratoryResultService.GetForReportAsync(
            startDate,
            endDate,
            searchTerm,
            testName,
            cancellationToken);

        return new LaboratoryResultsReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            SearchTerm = searchTerm?.Trim() ?? string.Empty,
            TestName = testName?.Trim() ?? string.Empty,
            LaboratoryResults = results
        };
    }
}
