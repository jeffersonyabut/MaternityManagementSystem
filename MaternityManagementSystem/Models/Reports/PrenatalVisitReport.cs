using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Models.Reports;

public class PrenatalVisitReport
{
    public DateTime GeneratedAtUtc { get; set; }

    public Patient Patient { get; set; } = null!;

    public Pregnancy Pregnancy { get; set; } = null!;

    public PrenatalVisit PrenatalVisit { get; set; } = null!;

    public List<VitalSign> VitalSigns { get; set; } = [];

    public List<LaboratoryResult> LaboratoryResults { get; set; } = [];

    public int TotalVitalSigns => VitalSigns.Count;

    public int TotalLaboratoryResults => LaboratoryResults.Count;
}
