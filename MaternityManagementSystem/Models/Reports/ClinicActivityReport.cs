namespace MaternityManagementSystem.Models.Reports;

public class ClinicActivityReport
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    // Overall activity totals
    public int TotalPatientRegistrations { get; set; }

    public int TotalPregnancyRecords { get; set; }

    public int TotalPrenatalVisits { get; set; }

    public int TotalLaboratoryResults { get; set; }

    public int UnassignedActivityDateCount { get; set; }

    public int TotalAppointments { get; set; }

    // Appointment status breakdown
    public int ScheduledAppointments { get; set; }

    public int ConfirmedAppointments { get; set; }

    public int CompletedAppointments { get; set; }

    public int CancelledAppointments { get; set; }

    public int NoShowAppointments { get; set; }

    // Unique patients with recorded activity
    public int UniquePatientsServed { get; set; }

    // Monthly activity for charts
    public List<ClinicActivityMonthlySummary> MonthlyActivity { get; set; } = [];
}

public class ClinicActivityMonthlySummary
{
    public string Month { get; set; } = string.Empty;

    public int PatientRegistrations { get; set; }

    public int PregnancyRecords { get; set; }

    public int PrenatalVisits { get; set; }

    public int LaboratoryResults { get; set; }

    public int Appointments { get; set; }
}
