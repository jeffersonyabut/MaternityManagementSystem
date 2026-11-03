using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Models.Reports;

public class PregnancySummaryReport
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string SearchTerm { get; set; } = string.Empty;

    public string Status { get; set; } = "All";

    public List<Pregnancy> Pregnancies { get; set; } = [];

    public int TotalPregnancies => Pregnancies.Count;

    public int UniquePatientCount =>
        Pregnancies
            .Select(pregnancy => pregnancy.PatientId)
            .Distinct()
            .Count();
}
