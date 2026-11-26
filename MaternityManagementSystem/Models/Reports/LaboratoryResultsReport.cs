using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Models.Reports;

public class LaboratoryResultsReport
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string SearchTerm { get; set; } = string.Empty;

    public string TestName { get; set; } = string.Empty;

    public List<LaboratoryResult> LaboratoryResults { get; set; } = [];

    public int TotalLaboratoryRecords => LaboratoryResults.Count;

    public int RequestedCount => CountByStatus(
        LaboratoryResultStatus.Requested);

    public int AwaitingResultCount => CountByStatus(
        LaboratoryResultStatus.AwaitingResult);

    public int ResultedCount => CountByStatus(
        LaboratoryResultStatus.Resulted);

    public int UnknownUnclassifiedCount =>
        LaboratoryResults.Count(result => result.Status is null);

    // Retained for existing callers while the report terminology changes.
    public int TotalResults => TotalLaboratoryRecords;

    private int CountByStatus(LaboratoryResultStatus status)
    {
        return LaboratoryResults.Count(result => result.Status == status);
    }
}
