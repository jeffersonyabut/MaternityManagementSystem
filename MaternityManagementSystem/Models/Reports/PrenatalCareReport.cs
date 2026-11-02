using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Models.Reports;

public class PrenatalCareReport
{
    // Report information
    public DateTime GeneratedAtUtc { get; set; }

    // Applied filters
    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? SearchTerm { get; set; }

    // Prenatal consultation records
    // Each visit contains its linked pregnancy and patient.
    // Vital signs are accessed through PrenatalVisit.VitalSigns.
    public List<PrenatalVisit> PrenatalVisits { get; set; } = [];

    // Summary counts
    public int TotalPrenatalVisits => PrenatalVisits.Count;

    public int TotalPatients =>
        PrenatalVisits
            .Where(v => v.Pregnancy != null)
            .Select(v => v.Pregnancy.PatientId)
            .Distinct()
            .Count();

    public int TotalVitalSigns =>
        PrenatalVisits.Sum(v => v.VitalSigns.Count);
}
