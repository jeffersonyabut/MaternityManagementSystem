using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Models.Reports;

public class AppointmentReport
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string SearchTerm { get; set; } = string.Empty;

    public string Status { get; set; } = "All";

    public List<Appointment> Appointments { get; set; } = [];

    public int TotalAppointments => Appointments.Count;

    public int UniquePatientCount =>
        Appointments
            .Select(appointment => appointment.PatientId)
            .Distinct()
            .Count();
}
