
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Models.Reports;

public class PatientMasterListReport
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Sex { get; set; }

    public string? SearchTerm { get; set; }

    public List<Patient> Patients { get; set; } = [];

    public int TotalPatients => Patients.Count;
}
