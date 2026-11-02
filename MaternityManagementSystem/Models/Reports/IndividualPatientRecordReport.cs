
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Models.Reports;

public class IndividualPatientRecordReport
{

    public ObstetricHistorySummary ObstetricHistory { get; set; } = new();
    // Report information
    public DateTime GeneratedAtUtc { get; set; }

    // Patient information
    public Patient Patient { get; set; } = null!;

    // Pregnancy history
    public List<Pregnancy> Pregnancies { get; set; } = [];

    // Prenatal consultation history
    public List<PrenatalVisit> PrenatalVisits { get; set; } = [];

    // Vital signs history
    public List<VitalSign> VitalSigns { get; set; } = [];

    // Laboratory history
    public List<LaboratoryResult> LaboratoryResults { get; set; } = [];

    // Summary counts
    public int TotalPregnancies => Pregnancies.Count;

    public int TotalPrenatalVisits => PrenatalVisits.Count;

    public int TotalVitalSigns => VitalSigns.Count;

    public int TotalLaboratoryResults => LaboratoryResults.Count;
}
