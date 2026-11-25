
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class AuditLogManagementViewModel : ObservableObject
{
    private const int PageSize = 10;

    private readonly RegionalSettingsService _regionalSettingsService;

    private readonly AuditLogService _auditLogService;
    private readonly StaffManagementService _staffManagementService;

    public AuditLogManagementViewModel(
        AuditLogService auditLogService,
        StaffManagementService staffManagementService,
        RegionalSettingsService regionalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(auditLogService);
        ArgumentNullException.ThrowIfNull(staffManagementService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        _auditLogService = auditLogService;
        _staffManagementService = staffManagementService;
        _regionalSettingsService = regionalSettingsService;
    }

    // AUDIT LOG COLLECTION

    [ObservableProperty]
    private ObservableCollection<AuditLog> auditLogs = new();

    [ObservableProperty]
    private AuditLog? selectedAuditLog;

    // STAFF FILTER

    [ObservableProperty]
    private ObservableCollection<AuditLogStaffFilterOption> staffOptions = new();

    [ObservableProperty]
    private AuditLogStaffFilterOption? selectedStaffOption;

    // ACTION AND ENTITY FILTERS

    [ObservableProperty]
    private ObservableCollection<string> actionOptions = new();

    [ObservableProperty]
    private string selectedAction = "All";

    [ObservableProperty]
    private ObservableCollection<string> entityTypeOptions = new();

    [ObservableProperty]
    private string selectedEntityType = "All";

    // SEARCH AND DATE FILTERS

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private DateTime? startDate;

    [ObservableProperty]
    private DateTime? endDate;

    // SUMMARY

    [ObservableProperty]
    private int totalActivities;

    [ObservableProperty]
    private int todayActivities;

    [ObservableProperty]
    private AuditLog? latestActivity;

    // PAGINATION

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalPages;

    [ObservableProperty]
    private int totalRecords;

    public string PaginationText =>
        TotalRecords == 0
            ? "No records found"
            : $"Page {CurrentPage} of {TotalPages} ({TotalRecords} records)";

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage =>
        TotalPages > 0 && CurrentPage < TotalPages;

    // GENERAL STATE

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool hasRecords;

    // LOAD INITIAL DATA / REFRESH

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = string.Empty;

        // Preserve the selected staff filter during refresh.
        int? previouslySelectedStaffId =
            SelectedStaffOption?.StaffId;

        try
        {
            var staff = await _staffManagementService.GetAllStaffAsync();
            var actions = await _auditLogService.GetAvailableActionsAsync();
            var entityTypes = await _auditLogService.GetAvailableEntityTypesAsync();
            var summary = await _auditLogService.GetSummaryAsync();

            StaffOptions.Clear();

            StaffOptions.Add(new AuditLogStaffFilterOption
            {
                DisplayName = "All Staff Accounts",
                StaffId = null
            });

            foreach (var member in staff.OrderBy(s => s.Username))
            {
                StaffOptions.Add(new AuditLogStaffFilterOption
                {
                    DisplayName = member.Username,
                    StaffId = member.Id
                });
            }

            SelectedStaffOption =
                StaffOptions.FirstOrDefault(
                    option => option.StaffId == previouslySelectedStaffId)
                ?? StaffOptions.FirstOrDefault();

            ActionOptions.Clear();
            ActionOptions.Add("All");

            foreach (var action in actions)
                ActionOptions.Add(action);

            EntityTypeOptions.Clear();
            EntityTypeOptions.Add("All");

            foreach (var entityType in entityTypes)
                EntityTypeOptions.Add(entityType);

            TotalActivities = summary.TotalActivities;
            TodayActivities = summary.TodayActivities;
            LatestActivity = summary.LatestActivity;

            CurrentPage = 1;

            await LoadPageAsync();

            StatusMessage = TotalRecords == 0
                ? "No audit activities have been recorded yet."
                : $"{TotalRecords} audit record(s) found.";
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (Exception)
        {
            AuditLogs.Clear();
            SelectedAuditLog = null;

            TotalActivities = 0;
            TodayActivities = 0;
            LatestActivity = null;

            TotalRecords = 0;
            TotalPages = 0;
            CurrentPage = 1;
            HasRecords = false;

            NotifyPaginationProperties();

            StatusMessage = "Unable to load audit activities.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // LOAD PAGINATED RECORDS

    private async Task LoadPageAsync()
    {
        AuditLogQuery query = new()
        {
            SearchTerm = SearchText,
            StaffId = SelectedStaffOption?.StaffId,
            Action = SelectedAction == "All"
                ? null
                : SelectedAction,
            EntityType = SelectedEntityType == "All"
                ? null
                : SelectedEntityType,
            StartDateUtc = ConvertStartDateToUtc(),
            EndDateUtc = ConvertEndDateToUtc(),
            Page = CurrentPage,
            PageSize = PageSize
        };

        AuditLogPage result =
            await _auditLogService.GetPageAsync(query);

        TotalRecords = result.TotalCount;
        TotalPages = result.TotalPages;

        // Keep the current page within valid pagination boundaries.
        int lastValidPage = Math.Max(1, TotalPages);

        if (CurrentPage > lastValidPage)
        {
            CurrentPage = lastValidPage;

            if (TotalPages > 0)
            {
                await LoadPageAsync();
                return;
            }
        }

        AuditLogs.Clear();
        SelectedAuditLog = null;

        foreach (var log in result.Items)
        {
            log.OccurredAtRegionalDisplay =
                _regionalSettingsService.FormatDateTime(
                    log.OccurredAtUtc);

            AuditLogs.Add(log);
        }

        HasRecords = AuditLogs.Count > 0;

        NotifyPaginationProperties();
    }

    // APPLY FILTERS

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        if (IsBusy)
            return;

        if (StartDate.HasValue &&
            EndDate.HasValue &&
            StartDate.Value.Date > EndDate.Value.Date)
        {
            StatusMessage = "Start date cannot be later than end date.";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        CurrentPage = 1;

        try
        {
            await LoadPageAsync();

            StatusMessage = HasRecords
                ? $"{TotalRecords} matching audit record(s) found."
                : "No matching audit records found.";
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (Exception)
        {
            StatusMessage = "Unable to apply audit log filters.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // PAGINATION

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (IsBusy || !HasNextPage)
            return;

        IsBusy = true;

        try
        {
            CurrentPage++;
            await LoadPageAsync();
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (Exception)
        {
            CurrentPage--;
            StatusMessage = "Unable to load the next page.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (IsBusy || !HasPreviousPage)
            return;

        IsBusy = true;

        try
        {
            CurrentPage--;
            await LoadPageAsync();
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (Exception)
        {
            CurrentPage++;
            StatusMessage = "Unable to load the previous page.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // CLEAR FILTERS

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        if (IsBusy)
            return;

        SearchText = string.Empty;
        SelectedStaffOption = StaffOptions.FirstOrDefault();
        SelectedAction = "All";
        SelectedEntityType = "All";
        StartDate = null;
        EndDate = null;

        await ApplyFiltersAsync();
    }

    // REFRESH

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    // PAGINATION NOTIFICATIONS

    private void NotifyPaginationProperties()
    {
        OnPropertyChanged(nameof(PaginationText));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
    }

    // DATE CONVERSION

    private DateTime? ConvertStartDateToUtc()
    {
        if (!StartDate.HasValue)
            return null;

        DateTime localDate = DateTime.SpecifyKind(
            StartDate.Value.Date,
            DateTimeKind.Unspecified);

        return _regionalSettingsService.ConvertLocalToUtc(
            localDate);
    }

    private DateTime? ConvertEndDateToUtc()
    {
        if (!EndDate.HasValue)
            return null;

        DateTime nextDay = DateTime.SpecifyKind(
            EndDate.Value.Date.AddDays(1),
            DateTimeKind.Unspecified);

        DateTime exclusiveEndUtc =
            _regionalSettingsService.ConvertLocalToUtc(nextDay);

        return exclusiveEndUtc.AddTicks(-1);
    }
}
