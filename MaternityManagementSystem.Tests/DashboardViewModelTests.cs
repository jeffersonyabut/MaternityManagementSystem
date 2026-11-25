
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Tests;

public class DashboardViewModelTests
{
    private static DashboardData CreateSampleData()
    {
        return new DashboardData
        {
            TotalPatients = 25,
            ActivePregnancies = 8,
            TodaysAppointments = 4,
            TotalPrenatalVisits = 32,

            MonthlyRegistrations =
            [
                new MonthlyStatistic { Label = "Feb 2026", Count = 2 },
                new MonthlyStatistic { Label = "Mar 2026", Count = 3 },
                new MonthlyStatistic { Label = "Apr 2026", Count = 4 },
                new MonthlyStatistic { Label = "May 2026", Count = 5 },
                new MonthlyStatistic { Label = "Jun 2026", Count = 6 },
                new MonthlyStatistic { Label = "Jul 2026", Count = 5 }
            ],

            MonthlyPrenatalVisits =
            [
                new MonthlyStatistic { Label = "Feb 2026", Count = 3 },
                new MonthlyStatistic { Label = "Mar 2026", Count = 4 },
                new MonthlyStatistic { Label = "Apr 2026", Count = 5 },
                new MonthlyStatistic { Label = "May 2026", Count = 6 },
                new MonthlyStatistic { Label = "Jun 2026", Count = 7 },
                new MonthlyStatistic { Label = "Jul 2026", Count = 7 }
            ],

            AppointmentDistribution =
            [
                new AppointmentStatusStatistic
                {
                    Status = "Scheduled",
                    Count = 4
                },
                new AppointmentStatusStatistic
                {
                    Status = "Confirmed",
                    Count = 3
                },
                new AppointmentStatusStatistic
                {
                    Status = "Completed",
                    Count = 8
                },
                new AppointmentStatusStatistic
                {
                    Status = "Cancelled",
                    Count = 2
                },
                new AppointmentStatusStatistic
                {
                    Status = "No-show",
                    Count = 1
                }
            ],

            UpcomingAppointments =
            [
                new UpcomingAppointment
                {
                    AppointmentId = 1,
                    PatientName = "Alice Test",
                    AppointmentDate = new DateOnly(2026, 7, 2),
                    StartTime = new TimeOnly(9, 0),
                    Purpose = "Prenatal checkup",
                    Status = "Scheduled"
                },
                new UpcomingAppointment
                {
                    AppointmentId = 2,
                    PatientName = "Bea Test",
                    AppointmentDate = new DateOnly(2026, 7, 2),
                    StartTime = new TimeOnly(10, 0),
                    Purpose = "Follow-up",
                    Status = "Confirmed"
                }
            ]
        };
    }

    [Fact]
    public void Constructor_WhenCreated_InitializesDefaultState()
    {
        var service = new FakeDashboardAnalyticsService();
        var viewModel = new DashboardViewModel(service);

        Assert.Equal(0, viewModel.TotalPatients);
        Assert.Equal(0, viewModel.ActivePregnancies);
        Assert.Equal(0, viewModel.TodaysAppointments);
        Assert.Equal(0, viewModel.TotalPrenatalVisits);

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasLoadedData);
        Assert.False(viewModel.IsInitialLoading);
        Assert.False(viewModel.IsRefreshing);

        Assert.False(viewModel.HasRegistrationData);
        Assert.False(viewModel.HasPrenatalVisitData);
        Assert.False(viewModel.HasAppointmentData);
        Assert.False(viewModel.HasUpcomingAppointments);

        Assert.Empty(viewModel.MonthlyRegistrations);
        Assert.Empty(viewModel.MonthlyPrenatalVisits);
        Assert.Empty(viewModel.AppointmentDistribution);
        Assert.Empty(viewModel.UpcomingAppointments);

        Assert.Empty(viewModel.RegistrationSeries);
        Assert.Empty(viewModel.PrenatalVisitSeries);
        Assert.Empty(viewModel.AppointmentStatusSeries);
    }

    [Fact]
    public void Constructor_WhenAnalyticsServiceIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DashboardViewModel(null!));
    }

    [Fact]
    public async Task LoadDashboardAsync_WhenSuccessful_PopulatesDashboardData()
    {
        var service = new FakeDashboardAnalyticsService
        {
            Handler = _ => Task.FromResult(CreateSampleData())
        };

        var viewModel = new DashboardViewModel(service);

        await viewModel.LoadDashboardAsync();

        Assert.Equal(25, viewModel.TotalPatients);
        Assert.Equal(8, viewModel.ActivePregnancies);
        Assert.Equal(4, viewModel.TodaysAppointments);
        Assert.Equal(32, viewModel.TotalPrenatalVisits);

        Assert.Equal(6, viewModel.MonthlyRegistrations.Count);
        Assert.Equal(6, viewModel.MonthlyPrenatalVisits.Count);
        Assert.Equal(5, viewModel.AppointmentDistribution.Count);
        Assert.Equal(2, viewModel.UpcomingAppointments.Count);

        Assert.True(viewModel.HasLoadedData);
        Assert.True(viewModel.HasRegistrationData);
        Assert.True(viewModel.HasPrenatalVisitData);
        Assert.True(viewModel.HasAppointmentData);
        Assert.True(viewModel.HasUpcomingAppointments);

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsInitialLoading);
        Assert.False(viewModel.IsRefreshing);

        Assert.Null(viewModel.ErrorMessage);

        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task LoadDashboardAsync_WhenSuccessful_UpdatesChartSeries()
    {
        var service = new FakeDashboardAnalyticsService
        {
            Handler = _ => Task.FromResult(CreateSampleData())
        };

        var viewModel = new DashboardViewModel(service);

        await viewModel.LoadDashboardAsync();

        Assert.Single(viewModel.RegistrationSeries);
        Assert.Single(viewModel.PrenatalVisitSeries);
        Assert.Equal(5, viewModel.AppointmentStatusSeries.Count);

        Assert.Equal(
            new[]
            {
                "Feb 2026",
                "Mar 2026",
                "Apr 2026",
                "May 2026",
                "Jun 2026",
                "Jul 2026"
            },
            viewModel.RegistrationXAxis[0].Labels);

        Assert.Equal(
            new[]
            {
                "Feb 2026",
                "Mar 2026",
                "Apr 2026",
                "May 2026",
                "Jun 2026",
                "Jul 2026"
            },
            viewModel.PrenatalXAxis[0].Labels);
    }

    [Fact]
    public async Task LoadDashboardAsync_WhenNoDataExists_SetsEmptyStateFlags()
    {
        var service = new FakeDashboardAnalyticsService
        {
            Handler = _ => Task.FromResult(new DashboardData())
        };

        var viewModel = new DashboardViewModel(service);

        await viewModel.LoadDashboardAsync();

        Assert.True(viewModel.HasLoadedData);

        Assert.False(viewModel.HasRegistrationData);
        Assert.False(viewModel.HasPrenatalVisitData);
        Assert.False(viewModel.HasAppointmentData);
        Assert.False(viewModel.HasUpcomingAppointments);

        Assert.Empty(viewModel.MonthlyRegistrations);
        Assert.Empty(viewModel.MonthlyPrenatalVisits);
        Assert.Empty(viewModel.AppointmentDistribution);
        Assert.Empty(viewModel.UpcomingAppointments);

        Assert.Single(viewModel.RegistrationSeries);
        Assert.Single(viewModel.PrenatalVisitSeries);
        Assert.Equal(5, viewModel.AppointmentStatusSeries.Count);

        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadDashboardAsync_WhenInitialLoadFails_DisplaysError()
    {
        var service = new FakeDashboardAnalyticsService
        {
            Handler = _ => throw new InvalidOperationException(
                "Test loading failure.")
        };

        var viewModel = new DashboardViewModel(service);

        await viewModel.LoadDashboardAsync();

        Assert.Equal(
            "Dashboard information could not be loaded. Please try again.",
            viewModel.ErrorMessage);

        Assert.False(viewModel.HasLoadedData);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsInitialLoading);
        Assert.False(viewModel.IsRefreshing);

        Assert.Equal(0, viewModel.TotalPatients);
        Assert.Empty(viewModel.MonthlyRegistrations);
    }

    [Fact]
    public async Task LoadDashboardAsync_WhenRefreshFails_PreservesExistingData()
    {
        int callCount = 0;

        var service = new FakeDashboardAnalyticsService
        {
            Handler = _ =>
            {
                callCount++;

                if (callCount == 1)
                {
                    return Task.FromResult(CreateSampleData());
                }

                throw new InvalidOperationException(
                    "Test refresh failure.");
            }
        };

        var viewModel = new DashboardViewModel(service);

        await viewModel.LoadDashboardAsync();

        await viewModel.LoadDashboardAsync();

        Assert.Equal(25, viewModel.TotalPatients);
        Assert.Equal(8, viewModel.ActivePregnancies);
        Assert.Equal(4, viewModel.TodaysAppointments);
        Assert.Equal(32, viewModel.TotalPrenatalVisits);

        Assert.Equal(6, viewModel.MonthlyRegistrations.Count);
        Assert.Equal(6, viewModel.MonthlyPrenatalVisits.Count);
        Assert.Equal(5, viewModel.AppointmentDistribution.Count);
        Assert.Equal(2, viewModel.UpcomingAppointments.Count);

        Assert.True(viewModel.HasLoadedData);
        Assert.True(viewModel.HasRegistrationData);
        Assert.True(viewModel.HasPrenatalVisitData);
        Assert.True(viewModel.HasAppointmentData);
        Assert.True(viewModel.HasUpcomingAppointments);

        Assert.Equal(
            "Dashboard information could not be loaded. Please try again.",
            viewModel.ErrorMessage);

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsRefreshing);

        Assert.Equal(2, service.CallCount);
    }

    [Fact]
    public async Task LoadDashboardAsync_WhenSessionExpires_PropagatesUnauthorizedAccessException()
    {
        var service = new FakeDashboardAnalyticsService
        {
            Handler = _ => throw new UnauthorizedAccessException(
                "Session expired.")
        };

        var viewModel = new DashboardViewModel(service);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => viewModel.LoadDashboardAsync());

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasLoadedData);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadDashboardAsync_WhenAlreadyLoading_DoesNotStartAnotherRequest()
    {
        var completionSource =
            new TaskCompletionSource<DashboardData>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var service = new FakeDashboardAnalyticsService
        {
            Handler = _ => completionSource.Task
        };

        var viewModel = new DashboardViewModel(service);

        Task firstLoad = viewModel.LoadDashboardAsync();

        Assert.True(viewModel.IsLoading);
        Assert.True(viewModel.IsInitialLoading);
        Assert.False(viewModel.IsRefreshing);

        await viewModel.LoadDashboardAsync();

        Assert.Equal(1, service.CallCount);
        Assert.True(viewModel.IsLoading);

        completionSource.SetResult(CreateSampleData());

        await firstLoad;

        Assert.Equal(1, service.CallCount);
        Assert.True(viewModel.HasLoadedData);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task LoadDashboardAsync_WhenRefreshing_KeepsExistingDataVisible()
    {
        var completionSource =
            new TaskCompletionSource<DashboardData>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int callCount = 0;

        var service = new FakeDashboardAnalyticsService
        {
            Handler = _ =>
            {
                callCount++;

                return callCount == 1
                    ? Task.FromResult(CreateSampleData())
                    : completionSource.Task;
            }
        };

        var viewModel = new DashboardViewModel(service);

        await viewModel.LoadDashboardAsync();

        Task refreshTask = viewModel.LoadDashboardAsync();

        Assert.True(viewModel.IsLoading);
        Assert.True(viewModel.IsRefreshing);
        Assert.False(viewModel.IsInitialLoading);

        Assert.Equal(25, viewModel.TotalPatients);
        Assert.Equal(6, viewModel.MonthlyRegistrations.Count);
        Assert.Equal(2, viewModel.UpcomingAppointments.Count);

        completionSource.SetResult(CreateSampleData());

        await refreshTask;

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsRefreshing);
        Assert.True(viewModel.HasLoadedData);
    }

    private sealed class FakeDashboardAnalyticsService
        : IDashboardAnalyticsService
    {
        public Func<CancellationToken, Task<DashboardData>> Handler
        { get; set; } =
                _ => Task.FromResult(new DashboardData());

        public int CallCount { get; private set; }

        public Task<DashboardData> GetDashboardDataAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Handler(cancellationToken);
        }
    }
}
