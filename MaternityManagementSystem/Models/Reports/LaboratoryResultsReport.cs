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

    public int TotalResults => LaboratoryResults.Count;
}
