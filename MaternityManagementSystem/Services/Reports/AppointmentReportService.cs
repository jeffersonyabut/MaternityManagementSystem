using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services.Reports;

public class AppointmentReportService
{
    private readonly AppointmentService _appointmentService;

    public AppointmentReportService(
        AppointmentService appointmentService)
    {
        ArgumentNullException.ThrowIfNull(appointmentService);

        _appointmentService = appointmentService;
    }

    public async Task<AppointmentReport> GenerateAppointmentReportAsync(
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

        var appointments =
            await _appointmentService.GetForReportAsync(
                startDate,
                endDate,
                searchTerm,
                normalizedStatus,
                cancellationToken);

        return new AppointmentReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            SearchTerm = searchTerm?.Trim() ?? string.Empty,
            Status = normalizedStatus,
            Appointments = appointments
        };
    }
}
