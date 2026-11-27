using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class AppointmentSmsReminderMonitoringViewModel : ObservableObject
{
    public const int DefaultPageSize = 50;

    private readonly IAppointmentSmsReminderMonitoringService _monitoringService;
    private AppliedFilters _appliedFilters = new(
        null,
        null,
        null,
        null,
        null);

    public AppointmentSmsReminderMonitoringViewModel(
        IAppointmentSmsReminderMonitoringService monitoringService)
    {
        ArgumentNullException.ThrowIfNull(monitoringService);

        _monitoringService = monitoringService;

        StatusFilterOptions.Add(null);
        foreach (AppointmentSmsReminderStatus status in
                 Enum.GetValues<AppointmentSmsReminderStatus>())
        {
            StatusFilterOptions.Add(status);
            StatusCounts.Add(new AppointmentSmsReminderStatusCount(status, 0));
        }

        ReminderTypeFilterOptions.Add(null);
        foreach (AppointmentSmsReminderType reminderType in
                 Enum.GetValues<AppointmentSmsReminderType>())
        {
            ReminderTypeFilterOptions.Add(reminderType);
        }
    }

    public ObservableCollection<AppointmentSmsReminderMonitoringRecord> Records { get; } = new();

    public ObservableCollection<AppointmentSmsReminderStatusCount> StatusCounts { get; } = new();

    public ObservableCollection<AppointmentSmsReminderStatus?> StatusFilterOptions { get; } = new();

    public ObservableCollection<AppointmentSmsReminderType?> ReminderTypeFilterOptions { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private AppointmentSmsReminderStatus? selectedStatus;

    [ObservableProperty]
    private AppointmentSmsReminderType? selectedReminderType;

    [ObservableProperty]
    private DateOnly? appointmentDateFrom;

    [ObservableProperty]
    private DateOnly? appointmentDateTo;

    [ObservableProperty]
    private int currentPageNumber = 1;

    [ObservableProperty]
    private int pageSize = DefaultPageSize;

    [ObservableProperty]
    private int totalRecords;

    [ObservableProperty]
    private int totalPages;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool HasPreviousPage => CurrentPageNumber > 1;

    public bool HasNextPage =>
        TotalPages > 0 && CurrentPageNumber < TotalPages;

    partial void OnCurrentPageNumberChanged(int value)
    {
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(HasNextPage));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        CurrentPageNumber = 1;
        await LoadCurrentPageAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadCurrentPageAsync();
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!ValidateFilters())
        {
            return;
        }

        _appliedFilters = CaptureFilters();
        CurrentPageNumber = 1;
        await LoadCurrentPageAsync();
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        if (IsBusy)
        {
            return;
        }

        SearchText = string.Empty;
        SelectedStatus = null;
        SelectedReminderType = null;
        AppointmentDateFrom = null;
        AppointmentDateTo = null;
        _appliedFilters = CaptureFilters();
        CurrentPageNumber = 1;

        await LoadCurrentPageAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (IsBusy || !HasPreviousPage)
        {
            return;
        }

        int previousPage = CurrentPageNumber;
        CurrentPageNumber--;

        if (!await LoadCurrentPageAsync())
        {
            CurrentPageNumber = previousPage;
        }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (IsBusy || !HasNextPage)
        {
            return;
        }

        int previousPage = CurrentPageNumber;
        CurrentPageNumber++;

        if (!await LoadCurrentPageAsync())
        {
            CurrentPageNumber = previousPage;
        }
    }

    private async Task<bool> LoadCurrentPageAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            AppointmentSmsReminderMonitoringPage page =
                await _monitoringService.GetMonitoringPageAsync(
                    CreateQuery(CurrentPageNumber));

            IReadOnlyList<AppointmentSmsReminderStatusCount> counts =
                await _monitoringService.GetMonitoringStatusCountsAsync();

            int effectivePageSize = page.PageSize;
            int pageCount = CalculateTotalPages(page.TotalCount, effectivePageSize);
            int lastValidPage = Math.Max(1, pageCount);

            if (CurrentPageNumber > lastValidPage)
            {
                CurrentPageNumber = lastValidPage;

                if (pageCount > 0)
                {
                    page = await _monitoringService.GetMonitoringPageAsync(
                        CreateQuery(CurrentPageNumber));
                    effectivePageSize = page.PageSize;
                    pageCount = CalculateTotalPages(
                        page.TotalCount,
                        effectivePageSize);
                }
            }

            Records.Clear();
            foreach (AppointmentSmsReminderMonitoringRecord record in page.Records)
            {
                Records.Add(record);
            }

            StatusCounts.Clear();
            foreach (AppointmentSmsReminderStatusCount count in counts)
            {
                StatusCounts.Add(count);
            }

            TotalRecords = page.TotalCount;
            PageSize = effectivePageSize;
            TotalPages = pageCount;

            if (CurrentPageNumber > Math.Max(1, TotalPages))
            {
                CurrentPageNumber = Math.Max(1, TotalPages);
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage =
                "Your session is not authorized to view SMS reminder monitoring.";
            return false;
        }
        catch (Exception)
        {
            ErrorMessage =
                "Unable to load SMS reminder monitoring data. Please try again.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ValidateFilters()
    {
        if (AppointmentDateFrom.HasValue &&
            AppointmentDateTo.HasValue &&
            AppointmentDateFrom.Value > AppointmentDateTo.Value)
        {
            ErrorMessage =
                "The appointment start date must be on or before the end date.";
            return false;
        }

        if (PageSize < 1)
        {
            ErrorMessage = "Page size must be at least 1.";
            return false;
        }

        return true;
    }

    private AppliedFilters CaptureFilters()
    {
        return new AppliedFilters(
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            SelectedStatus,
            SelectedReminderType,
            AppointmentDateFrom,
            AppointmentDateTo);
    }

    private AppointmentSmsReminderMonitoringQuery CreateQuery(int pageNumber)
    {
        return new AppointmentSmsReminderMonitoringQuery
        {
            Status = _appliedFilters.Status,
            ReminderType = _appliedFilters.ReminderType,
            AppointmentDateFrom = _appliedFilters.AppointmentDateFrom,
            AppointmentDateTo = _appliedFilters.AppointmentDateTo,
            SearchText = _appliedFilters.SearchText,
            PageNumber = pageNumber,
            PageSize = PageSize
        };
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return pageSize < 1
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private sealed record AppliedFilters(
        string? SearchText,
        AppointmentSmsReminderStatus? Status,
        AppointmentSmsReminderType? ReminderType,
        DateOnly? AppointmentDateFrom,
        DateOnly? AppointmentDateTo);
}
