
using System.Threading.Tasks;
using System.Threading;


namespace MaternityManagementSystem.Services;

public interface IDashboardAnalyticsService
{
    Task<DashboardData> GetDashboardDataAsync(
        CancellationToken cancellationToken = default);
}
