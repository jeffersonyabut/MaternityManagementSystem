using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Tests;

public sealed class AppointmentSmsReminderMonitoringViewModelTests
{
    private sealed class FakeMonitoringService : IAppointmentSmsReminderMonitoringService
    {
        public Func<AppointmentSmsReminderMonitoringQuery,
            Task<AppointmentSmsReminderMonitoringPage>> PageHandler { get; set; } =
            query => Task.FromResult(CreatePage(query.PageNumber, 0, query.PageSize));

        public Func<Task<IReadOnlyList<AppointmentSmsReminderStatusCount>>> CountsHandler { get; set; } =
            () => Task.FromResult(CreateCounts());

        public List<AppointmentSmsReminderMonitoringQuery> Queries { get; } = new();

        public int StatusCountCallCount { get; private set; }

        public async Task<AppointmentSmsReminderMonitoringPage> GetMonitoringPageAsync(
            AppointmentSmsReminderMonitoringQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return await PageHandler(query);
        }

        public async Task<IReadOnlyList<AppointmentSmsReminderStatusCount>>
            GetMonitoringStatusCountsAsync(
                CancellationToken cancellationToken = default)
        {
            StatusCountCallCount++;
            return await CountsHandler();
        }
    }

    private static AppointmentSmsReminderMonitoringPage CreatePage(
        int pageNumber,
        int totalCount,
        int pageSize,
        params AppointmentSmsReminderMonitoringRecord[] records)
    {
        return new AppointmentSmsReminderMonitoringPage(
            records,
            totalCount,
            pageNumber,
            pageSize);
    }

    private static IReadOnlyList<AppointmentSmsReminderStatusCount> CreateCounts()
    {
        return Enum.GetValues<AppointmentSmsReminderStatus>()
            .Select(status => new AppointmentSmsReminderStatusCount(status, 0))
            .ToArray();
    }

    [Fact]
    public async Task LoadAsync_LoadsFirstPageAndAllStatusCounts()
    {
        var service = new FakeMonitoringService
        {
            PageHandler = query => Task.FromResult(CreatePage(
                query.PageNumber,
                72,
                query.PageSize))
        };
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Single(service.Queries);
        Assert.Equal(1, service.Queries[0].PageNumber);
        Assert.Equal(50, service.Queries[0].PageSize);
        Assert.Equal(7, viewModel.StatusCounts.Count);
        Assert.Equal(2, viewModel.TotalPages);
        Assert.Equal(72, viewModel.TotalRecords);
        Assert.Equal(1, service.StatusCountCallCount);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RefreshAsync_ReloadsCurrentPageAndStatusCounts()
    {
        var service = new FakeMonitoringService
        {
            PageHandler = query => Task.FromResult(CreatePage(
                query.PageNumber,
                120,
                query.PageSize))
        };
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service);

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.NextPageCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(new[] { 1, 2, 2 },
            service.Queries.Select(query => query.PageNumber));
        Assert.Equal(3, service.StatusCountCallCount);
    }

    [Fact]
    public async Task ApplyFiltersAsync_ResetsPageAndSubmitsSelectedFilters()
    {
        var service = new FakeMonitoringService
        {
            PageHandler = query => Task.FromResult(CreatePage(
                query.PageNumber,
                120,
                query.PageSize))
        };
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service)
        {
            SearchText = "Mia Santos",
            SelectedStatus = AppointmentSmsReminderStatus.Failed,
            SelectedReminderType = AppointmentSmsReminderType.Final24Hour,
            AppointmentDateFrom = new DateOnly(2026, 12, 1),
            AppointmentDateTo = new DateOnly(2026, 12, 31)
        };

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.NextPageCommand.ExecuteAsync(null);
        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        AppointmentSmsReminderMonitoringQuery query = service.Queries[^1];
        Assert.Equal(1, query.PageNumber);
        Assert.Equal("Mia Santos", query.SearchText);
        Assert.Equal(AppointmentSmsReminderStatus.Failed, query.Status);
        Assert.Equal(AppointmentSmsReminderType.Final24Hour, query.ReminderType);
        Assert.Equal(new DateOnly(2026, 12, 1), query.AppointmentDateFrom);
        Assert.Equal(new DateOnly(2026, 12, 31), query.AppointmentDateTo);
    }

    [Fact]
    public async Task ClearFiltersAsync_RestoresFilterDefaultsAndPageOne()
    {
        var service = new FakeMonitoringService
        {
            PageHandler = query => Task.FromResult(CreatePage(
                query.PageNumber,
                120,
                query.PageSize))
        };
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service)
        {
            SearchText = "Ada",
            SelectedStatus = AppointmentSmsReminderStatus.Unknown,
            SelectedReminderType = AppointmentSmsReminderType.Advance3Day,
            AppointmentDateFrom = new DateOnly(2026, 12, 1),
            AppointmentDateTo = new DateOnly(2026, 12, 31)
        };

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.NextPageCommand.ExecuteAsync(null);
        await viewModel.ClearFiltersCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Null(viewModel.SelectedStatus);
        Assert.Null(viewModel.SelectedReminderType);
        Assert.Null(viewModel.AppointmentDateFrom);
        Assert.Null(viewModel.AppointmentDateTo);
        Assert.Equal(1, viewModel.CurrentPageNumber);

        AppointmentSmsReminderMonitoringQuery query = service.Queries[^1];
        Assert.Null(query.SearchText);
        Assert.Null(query.Status);
        Assert.Null(query.ReminderType);
        Assert.Null(query.AppointmentDateFrom);
        Assert.Null(query.AppointmentDateTo);
        Assert.Equal(1, query.PageNumber);
    }

    [Fact]
    public async Task PageCommands_RespectPaginationBoundaries()
    {
        var service = new FakeMonitoringService
        {
            PageHandler = query => Task.FromResult(CreatePage(
                query.PageNumber,
                75,
                query.PageSize))
        };
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service);

        await viewModel.PreviousPageCommand.ExecuteAsync(null);
        Assert.Empty(service.Queries);

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.PreviousPageCommand.ExecuteAsync(null);
        Assert.Single(service.Queries);

        await viewModel.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, viewModel.CurrentPageNumber);
        await viewModel.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, service.Queries.Count);

        await viewModel.PreviousPageCommand.ExecuteAsync(null);
        Assert.Equal(1, viewModel.CurrentPageNumber);
        await viewModel.PreviousPageCommand.ExecuteAsync(null);
        Assert.Equal(3, service.Queries.Count);
    }

    [Fact]
    public async Task SearchText_IsSubmittedOnlyAfterFiltersAreExplicitlyApplied()
    {
        var service = new FakeMonitoringService();
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service);

        viewModel.SearchText = "Mia";
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Null(service.Queries[^1].SearchText);

        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);
        Assert.Equal("Mia", service.Queries[^1].SearchText);

        viewModel.SearchText = "Unapplied edit";
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Mia", service.Queries[^1].SearchText);
    }

    [Fact]
    public async Task ApplyFiltersAsync_WithInvalidDateRange_DoesNotCallService()
    {
        var service = new FakeMonitoringService();
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service)
        {
            AppointmentDateFrom = new DateOnly(2026, 12, 2),
            AppointmentDateTo = new DateOnly(2026, 12, 1)
        };

        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Empty(service.Queries);
        Assert.Equal(0, service.StatusCountCallCount);
        Assert.Contains("start date", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoadAsync_WhenServiceThrows_ShowsUserFacingErrorAndClearsBusy()
    {
        var service = new FakeMonitoringService
        {
            PageHandler = _ => throw new InvalidOperationException("private details")
        };
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Contains("Unable to load", viewModel.ErrorMessage);
        Assert.DoesNotContain("private details", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoadAsync_ManagesBusyStateAfterSuccess()
    {
        var completion = new TaskCompletionSource<AppointmentSmsReminderMonitoringPage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeMonitoringService
        {
            PageHandler = _ => completion.Task
        };
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service);

        Task loadTask = viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsBusy);
        completion.SetResult(CreatePage(1, 0, 50));
        await loadTask;

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task OverlappingRequests_AreIgnoredWhileBusy()
    {
        var completion = new TaskCompletionSource<AppointmentSmsReminderMonitoringPage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeMonitoringService
        {
            PageHandler = _ => completion.Task
        };
        var viewModel = new AppointmentSmsReminderMonitoringViewModel(service);

        Task loadTask = viewModel.LoadCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsBusy);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Single(service.Queries);
        Assert.Equal(0, service.StatusCountCallCount);

        completion.SetResult(CreatePage(1, 0, 50));
        await loadTask;

        Assert.False(viewModel.IsBusy);
        Assert.Equal(1, service.StatusCountCallCount);
    }
}
