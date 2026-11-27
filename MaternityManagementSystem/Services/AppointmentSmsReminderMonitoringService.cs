using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public sealed class AppointmentSmsReminderMonitoringService
    : IAppointmentSmsReminderMonitoringService
{
    private readonly AppointmentSmsReminderRepository _reminderRepository;
    private readonly AuthorizationService _authorizationService;

    public AppointmentSmsReminderMonitoringService(
        AppointmentSmsReminderRepository reminderRepository,
        AuthorizationService authorizationService)
    {
        ArgumentNullException.ThrowIfNull(reminderRepository);
        ArgumentNullException.ThrowIfNull(authorizationService);

        _reminderRepository = reminderRepository;
        _authorizationService = authorizationService;
    }

    public async Task<AppointmentSmsReminderMonitoringPage> GetMonitoringPageAsync(
        AppointmentSmsReminderMonitoringQuery query,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAdminAsync();

        ArgumentNullException.ThrowIfNull(query);

        return await _reminderRepository.GetMonitoringPageAsync(
            query,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentSmsReminderStatusCount>>
        GetMonitoringStatusCountsAsync(
            CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAdminAsync();

        return await _reminderRepository.GetMonitoringStatusCountsAsync(
            cancellationToken);
    }
}
