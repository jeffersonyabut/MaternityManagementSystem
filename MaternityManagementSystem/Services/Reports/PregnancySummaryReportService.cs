using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services.Reports;

public class PregnancySummaryReportService
{
    private readonly PregnancyService _pregnancyService;

    public PregnancySummaryReportService(
        PregnancyService pregnancyService)
    {
        ArgumentNullException.ThrowIfNull(pregnancyService);

        _pregnancyService = pregnancyService;
    }

    public async Task<PregnancySummaryReport>
        GeneratePregnancySummaryReportAsync(
            DateOnly? startDate,
            DateOnly? endDate,
            string? searchTerm,
            string? status,
            CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        string normalizedStatus =
            string.IsNullOrWhiteSpace(status)
                ? "All"
                : status.Trim();

        var pregnancies =
            await _pregnancyService.GetForReportAsync(
                startDate,
                endDate,
                searchTerm,
                normalizedStatus,
                cancellationToken);

        return new PregnancySummaryReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            SearchTerm = searchTerm?.Trim() ?? string.Empty,
            Status = normalizedStatus,
            Pregnancies = pregnancies
        };
    }
}
