using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public interface IAppointmentSmsReminderMonitoringService
{
    Task<AppointmentSmsReminderMonitoringPage> GetMonitoringPageAsync(
        AppointmentSmsReminderMonitoringQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentSmsReminderStatusCount>>
        GetMonitoringStatusCountsAsync(
            CancellationToken cancellationToken = default);
}
